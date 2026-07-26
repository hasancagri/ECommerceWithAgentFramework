# Feature Specification: Stok Rezervasyonu (Model B)

**Feature Branch**: `012-stock-reservation`

**Created**: 2026-07-24

**Status**: Draft

**Input**: Kullanıcı isteği — sepete eklerken TTL'li stok rezervasyonu, sepette adet
(Quantity), sipariş anında gerçek stok düşürme; tedarikçi feed'i stoğu ezmez.

## Clarifications

### Session 2026-07-24

- Q: TTL yenilenmesi — sepet hareketinde süre sıfırlanır mı? → A: Sabit; yenileme yok
  (ilk eklemeden itibaren sabit, sonraki hareketler süreyi uzatmaz — Passo gibi).
- Q: Sipariş anında rezervasyon süresi dolmuşsa? → A: Süresi dolan rezervasyon sepetten
  çıkarılır; sipariş yalnızca geçerli rezervasyonu olan ürünleri içerir, Commit doğrular.
- Q: Sepetten çıkarma / TTL dolumu neyi etkiler? → A: Yalnızca Reserved azalır (Available
  yükselir); OnHand (fiziksel) sabit kalır. Adetler int sayaç; enum değil.
- Q: TTL dolunca sepet satırı nasıl temizlenir? → A: Event ile — Stock `ReservationExpired`
  yayınlar, Basket tüketip sepet satırını siler.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Son ürün sepete atan kullanıcıya ayrılır (Priority: P1)

Stokta 1 adet olan bir üründe, kullanıcı A ürünü sepete attığında o adet A'ya ayrılır;
kullanıcı B aynı ürünü sepete atmaya çalıştığında "stokta yok" görür.

**Why this priority**: Feature'ın çekirdeği. Aşırı-satışı (oversell) önleyen tek
davranış budur; onsuz stok kontrolü anlamsızdır.

**Independent Test**: İki kullanıcıyla, tek stoklu bir ürünü aynı anda sepete atmayı
dene; yalnızca ilkinin başarılı olduğu, ikincisinin reddedildiği doğrulanır.

**Acceptance Scenarios**:

1. **Given** stokta 1 adet ürün, **When** A ürünü sepete ekler, **Then** ekleme
   başarılı ve ürün A'ya ayrılır (Available 0'a düşer).
2. **Given** A son adedi sepetinde tutuyor, **When** B aynı ürünü sepete eklemeye
   çalışır, **Then** işlem "stokta yok" (yetersiz stok) ile reddedilir.
3. **Given** iki kullanıcı aynı anda son adedi ekler, **When** yarış oluşur, **Then**
   yalnızca biri başarılı olur, diğeri reddedilir (çift satış olmaz).

---

### User Story 2 - Sipariş verilince stok gerçekten düşer (Priority: P1)

Kullanıcı sepetindeki ürünlerle sipariş verdiğinde, ilgili stok kalıcı olarak azalır ve
rezervasyon kapatılır; başka kullanıcılar için Available buna göre güncellenir.

**Why this priority**: Bugün sipariş anında stok hiç düşmüyor. Rezervasyon geçici; onu
kalıcı düşüşe çeviren adım olmadan stok gerçeği hiç değişmez.

**Independent Test**: Bir ürünü sipariş et; sipariş sonrası stok adedinin sipariş
miktarı kadar azaldığı ve rezervasyonun kapandığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** kullanıcı 2 adet ürünü sepetinde rezerve etti, **When** sipariş verir,
   **Then** OnHand 2 azalır ve o rezervasyon kapanır (Commit).
2. **Given** sipariş oluştu, **When** stok sorgulanır, **Then** Available = OnHand −
   kalan aktif rezervasyonlar olarak doğru döner.

---

### User Story 3 - Sepette adet yönetimi (Quantity) (Priority: P2)

Kullanıcı bir üründen sepete birden fazla adet ekleyebilir, adedi artırıp azaltabilir;
üst sınır o an mevcut olan (Available) stoktur.

**Why this priority**: Bugün sepet her üründen 1 adet tutuyor. Gerçek alışveriş için
adet şart; ayrıca üst sınır kontrolü rezervasyonla tutarlı olmalı.

**Independent Test**: Stokta 5 adet ürüne sepetten 3 ekle, 5'e çıkar, 6 denemesi
reddedilir; her değişimde rezervasyonun adetle eşleştiği doğrulanır.

**Acceptance Scenarios**:

1. **Given** stokta 5 adet, **When** kullanıcı sepete 3 adet ekler, **Then** 3 adet
   rezerve edilir, Available 2 olur.
2. **Given** sepette 3 adet var, **When** kullanıcı adedi 5'e çıkarır, **Then** toplam
   5 rezerve edilir, Available 0 olur.
3. **Given** Available 0, **When** kullanıcı bir adet daha eklemeye çalışır, **Then**
   işlem yetersiz stok ile reddedilir.
4. **Given** sepette 5 adet var, **When** kullanıcı adedi 2'ye düşürür, **Then** 3 adet
   serbest bırakılır, Available 3 olur.

---

### User Story 4 - Süresi dolan rezervasyon serbest kalır (Priority: P2)

Kullanıcı ürünü sepete atıp belirlenen süre (TTL) içinde sipariş vermezse, ayrılan stok
otomatik serbest bırakılır ve başka kullanıcılar tekrar satın alabilir.

**Why this priority**: TTL olmadan terk edilmiş sepetler stoğu süresiz kilitler.
Serbest bırakma, rezervasyon modelinin sürdürülebilirliğini sağlar.

**Independent Test**: TTL'i kısa ayarla; ürünü sepete at, süre dolmasını bekle;
Available'ın geri arttığı ve başka kullanıcının satın alabildiği doğrulanır.

**Acceptance Scenarios**:

1. **Given** A ürünü sepete attı ve TTL doldu, **When** stok sorgulanır, **Then** o
   rezervasyon Available hesabına dahil edilmez (geri döner).
2. **Given** A'nın rezervasyonu süresi doldu, **When** B aynı ürünü sepete ekler,
   **Then** ekleme başarılı olur.
3. **Given** kullanıcı ürünü sepetten çıkarır, **When** işlem tamamlanır, **Then**
   rezervasyon anında (TTL beklemeden) serbest bırakılır.

---

### User Story 5 - Kullanıcıya iki sayaç gösterilir (Priority: P3)

Kullanıcı sepette rezervasyonunun ne kadar süre geçerli olduğunu geri sayan bir sayaç
görür; ürün/sepet ekranında kalan stok adedini ("son N adet") görür.

**Why this priority**: Şeffaflık ve aciliyet hissi UX'i iyileştirir, ama çekirdek stok
doğruluğu için zorunlu değildir; bu yüzden P3.

**Independent Test**: Ürünü sepete at; sepette süre geri sayan bir sayaç ve ürün
ekranında kalan-adet göstergesi görüntülendiği doğrulanır.

**Acceptance Scenarios**:

1. **Given** kullanıcı ürünü rezerve etti, **When** sepeti görüntüler, **Then** kalan
   rezervasyon süresini gösteren bir geri sayım görür.
2. **Given** üründe düşük stok var, **When** kullanıcı ürünü/sepeti görüntüler, **Then**
   kalan adet ("son N adet") gösterilir.

---

### Edge Cases

- **Tedarikçi güncellemesi aktif rezervasyonların altına düşerse (oversell):** Available
  0'a kırpılır, mevcut rezervasyonlara dokunulmaz, durum log'a yazılır; otomatik iptal
  yoktur (bilinçli, kapsam dışı).
- **Süresi dolmuş ama fiziksel silinmemiş rezervasyon:** Available hesabına dahil
  edilmez (lazy filtre); arka plan temizliği kaydı sonradan siler.
- **Rezervasyon süresi sepette dolar:** İlgili sepet satırı `ReservationExpired` event'i
  ile silinir (ölü satır kalmaz); kullanıcı isterse ürünü yeniden ekler (stok varsa).
- **Aynı kullanıcı aynı ürünü tekrar ekler:** Rezervasyon o kullanıcı+ürün için tek
  girdi olarak adetle güncellenir (yeni ayrı hold açılmaz).
- **Stock servisi erişilemezse (senkron çağrı):** Sepete ekleme güvenli tarafta
  reddedilir; kullanıcı hatayı görür (fail-closed, oversell'e izin verilmez).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, kullanıcı ürünü sepete eklediğinde/adedini artırdığında stoktan o
  adet kadar TTL'li rezervasyon yapmalıdır.
- **FR-002**: Sistem, `Available = OnHand − aktif (süresi geçmemiş) rezervasyonlar`
  olarak hesaplamalı ve bu değeri sorgulanabilir kılmalıdır.
- **FR-003**: Sistem, istenen adet mevcut Available'ı aşarsa sepete ekleme/artırma
  işlemini yetersiz stok ile reddetmelidir.
- **FR-004**: Rezervasyon bütünlüğü ve "stok negatif olamaz" invariant'ı Stock
  aggregate'i içinde korunmalıdır; eşzamanlı son-ürün yarışı çift satışa yol açmamalıdır.
- **FR-005**: Sistem, kullanıcı ürünü sepetten çıkardığında/adedini azalttığında ilgili
  rezervasyonu anında (TTL beklemeden) serbest bırakmalıdır.
- **FR-006**: Sistem, TTL süresi dolan rezervasyonu Available hesabına dahil etmemeli ve
  arka planda fiziksel olarak temizlemelidir.
- **FR-006a**: Rezervasyon serbest bırakma/çıkarma/TTL-dolumu yalnızca Reserved'ı
  azaltır; OnHand (fiziksel stok) değişmez. Adetler tam sayı (int) sayaçtır.
- **FR-006b**: TTL dolduğunda Stock bir bildirim (ReservationExpired) yayınlamalı; Basket
  bunu tüketip ilgili sepet satırını silmelidir (ölü satır sepette kalmaz).
- **FR-007**: Sistem, sipariş oluşturulduğunda ilgili rezervasyonu kalıcı stok düşüşüne
  çevirmeli (Commit): OnHand azalır, rezervasyon kapanır.
- **FR-008**: Sipariş, sipariş anında yeterli stok/rezervasyon yoksa oluşturulmamalıdır
  (kalıcı düşüş yalnızca geçerli rezervasyon üzerinden yapılır).
- **FR-008a**: Süresi dolan rezervasyon sepetten çıkarıldığı için sipariş yalnızca hâlâ
  geçerli rezervasyonu olan ürünleri içerir; Commit anı stoğu son kez doğrular.
- **FR-008b**: Görüntüleme için Available'dan türetilmiş bir stok durumu (ör.
  OutOfStock/LowStock/InStock) UI'a sunulabilir; bu yalnızca gösterimdir, adet hesabı hep sayısaldır.
- **FR-009**: `BasketItem` bir adet (Quantity) taşımalı; ekleme adeti artırır, üst sınır
  o an mevcut Available stoktur.
- **FR-010**: Rezervasyon TTL süresi yapılandırmadan (appsettings) okunmalı, varsayılan
  15 dakika olmalı, ortama göre değiştirilebilmelidir.
- **FR-010a**: TTL **sabittir**: `ExpiresAt` ilk eklemede belirlenir; sonraki sepet
  hareketleri (adet artırma/azaltma) süreyi **yenilemez/uzatmaz**.
- **FR-011**: Bir kullanıcının bir ürün için rezervasyonu tek girdi olarak tutulmalı ve
  sepetteki adetle eşlenmelidir (mükerrer hold açılmaz).
> **[SUPERSEDED — 014-supplier-stock-authority]** Aşağıdaki FR-012/013/014 "Model C"
> kararı (feed stoğu ezmez) 014 ile TERSİNE döndü: tedarikçi feed'i artık stoğun tek
> otoritesidir ve OnHand'i mutlak değere eşitler (create+update). Güncel davranış için
> 014 spec'ine bakın; bu üç madde tarihsel olarak korunur.

- **FR-012**: Tedarikçi feed'i, mevcut bir ürünün stok adedini (OnHand) EZMEMELİDİR;
  feed yalnızca fiyat/açıklama/indirim gibi tedarikçiye ait alanları güncelleyebilir.
- **FR-013**: Stok yalnızca ürün ilk oluşumunda tedarikçi verisinden seed edilmeli;
  sonraki tedarikçi snapshot'ları stok adedine dokunmamalıdır.
- **FR-014**: Yeniden mal alımı (restock) yalnızca açık (manuel/agent) bir işlemle
  yapılmalı; tedarikçi feed'i otomatik restock tetiklememelidir.
- **FR-015**: Kullanıcı sepette rezervasyonunun kalan süresini geri sayan bir sayaç
  görebilmelidir (rezervasyonun bitiş zamanı arayüze açılır).
- **FR-016**: Kullanıcı ürün/sepet ekranında kalan stok adedini ("son N adet")
  görebilmelidir.
- **FR-017**: Tedarikçi güncellemesi aktif rezervasyonların altına düşerse sistem
  Available'ı 0'a kırpmalı, mevcut rezervasyonları iptal etmemeli, durumu log'lamalıdır.
- **FR-018**: Sepete ekleme sırasında stok tarafına ulaşılamazsa işlem güvenli tarafta
  reddedilmeli (fail-closed); belirsiz durumda oversell'e izin verilmez.

### Key Entities *(include if feature involves data)*

- **ProductStock (Stock context aggregate)**: Bir ürünün stok gerçeğini tutar. OnHand
  (int, fiziksel adet) + aktif rezervasyon girdileri. `Available = OnHand − aktif Reserved`.
- **StockReservation (ProductStock içinde girdi)**: Bir kullanıcının bir ürün için
  ayırdığı adet (int) + bitiş zamanı (ExpiresAt). Reserve/Release/Commit ile yönetilir.
- **BasketItem (Basket context)**: Sepetteki bir ürün satırı; yeni Quantity (int) alanı.
- **Order (Order context)**: Sipariş oluşumunda (ödeme sonrası) stok Commit'ini tetikler.
- **ReservationExpired (integration event, Stock→Basket)**: TTL dolunca yayınlanır;
  Basket ilgili sepet satırını siler.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tek stoklu bir ürünü aynı anda iki kullanıcı sepete atmaya çalıştığında,
  vakaların %100'ünde yalnızca biri başarılı olur (çift satış 0).
- **SC-002**: Sipariş verildikten sonra ürünün stok adedi, vakaların %100'ünde sipariş
  miktarı kadar azalmış olur.
- **SC-003**: Terk edilen bir sepetteki rezervasyon, TTL dolduktan sonra stoğa geri
  döner ve ürün yeniden satın alınabilir hale gelir.
- **SC-004**: Stokta bulunmayan (Available 0) bir ürün hiçbir kullanıcının sepetine
  eklenemez; deneme net bir "stokta yok" mesajıyla reddedilir.
- **SC-005**: Tedarikçi feed'i bir güncelleme yayınladığında, daha önce satılmış/ayrılmış
  stok geri gelmez (mevcut OnHand ezilmez).
- **SC-006**: Kullanıcı sepette rezervasyon süresini ve ürün ekranında kalan adedi
  görebilir.

## Assumptions

- **Senkron koordinasyon (gRPC):** Sepete-ekleme anında stok kararı senkron olmalı;
  Basket, Stock'u **gRPC** ile çağırır (MCP servisler-arası kullanılmaz — kullanıcı kararı).
- **Anayasa amendment'ı gerekir:** gRPC/HttpClient, mevcut "yalnızca event + MCP"
  kanallarına ait değil; yeni senkron kanal bilinçli bir anayasa değişikliğiyle eklenir.
- **Rezervasyon Stock context'inde yaşar:** Çünkü bir ürünün tüm kullanıcı hold'ları
  ancak orada topluca görülüp çekişme çözülebilir; Basket tek kullanıcının sepetini görür.
- **Commit ödeme sonrasıdır:** Mevcut akışta sipariş, ödeme yapıldıktan sonra "Paid"
  olarak oluşuyor; stok Commit'i bu anda yapılır. Refund/askı senaryosu kapsam dışı.
- **TTL temizliği:** Süresi geçmiş rezervasyonların fiziksel silimi mevcut zamanlayıcı
  altyapısıyla (008 Hangfire) yapılır; lazy filtre görünürlük doğruluğunu sağlar.
- **Kullanıcı kimliği:** Rezervasyon, sepetin sahibi kullanıcı kimliğine bağlanır
  (anonim sepetler dahil, mevcut kimlik çözümü yeniden kullanılır). **Bağımlılık (U1):**
  anonim kullanıcının BFF token'ında stabil/benzersiz `sub` bulunmalı; yoksa `Guid.Empty`
  çakışması anon'lar arasında paylaşılan hold'a yol açar.
- **Yetki (G2):** Basket→Stock ve Order→Stock gRPC çağrıları için çağıranın token'ında
  `stock.reserve` scope'u bulunmalı; WebApp BFF (anon dahil) bu scope'u talep eder.
- **Adlandırma (I1):** Fiziksel stok kalıcı olarak `Quantity` alanında tutulur; domain
  `OnHand` semantiğini expose eder, dış API `onHand`/`available` kullanır (aynı değer).
- **Kapsam dışı (bilinçli, YAGNI):** Oversell otomatik çözümü, tedarikçi bazlı satış
  raporu, lot/batch izleme, indirim/redemption (ayrı feature).