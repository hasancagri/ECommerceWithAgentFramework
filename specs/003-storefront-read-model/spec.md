# Feature Specification: Storefront Composite Read Model (Ürün Vitrin Görünümü)

**Feature Branch**: `003-storefront-read-model`

**Created**: 2026-07-19

**Status**: Draft

**Input**: User description: "Catalog, Stock ve Discount context'lerinden gelen veriyi
ürün (ProductId) bazında birleştiren materialize edilmiş composite read model. Ürün
vitrin görünümü: ürün adı, görsel, stok durumu (stokta/tükendi), ürüne özel indirim
oranı. Writer-publishes, event-tetikli beslenme; kendi DB/servisi (Storefront), başka
servisle paylaşılmaz. Görünüm herkese açıktır, kullanıcıya özel değildir — sahiplik/yetki
kontrolü yoktur. Sipariş ve ödeme bu feature'ın kapsamı dışındadır. Discount.Api'nin
bugünkü kullanıcı-bazlı (sipariş sonrası otomatik üretilen ödül kuponu) modeli, ürün-bazlı
indirime dönüştürülür — bu dönüşüm bu feature'ın kapsamındadır."

## Artefakt Ölçekleme Kademesi

**Tam (Full)**. Gerekçe: yeni bir bounded context (yeni servis + yeni DB + yeni şema),
servisler-arası yeni "changed" integration event'leri, yeni bir sorgu kontratı VE
`Discount.Api`'nin aggregate'inde yeni bir iş kuralı (kullanıcı-bazlı → ürün-bazlı model
değişikliği) getiriyor. Anayasa: "yeni aggregate/tablo, servisler-arası event, yeni
kontrat veya yeni iş kuralı → tam akış."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ürün vitrin bilgisi tek çağrıda birleşik döner (Priority: P1)

Bir ziyaretçi (kimlik doğrulaması gerekmez) bir ürünün vitrin bilgisini görüntülediğinde;
ürün adı, görseli, stok durumu ve ürüne tanımlıysa indirim oranı **tek bir okumada**
birleşik döner — istemci Catalog/Stock/Discount'u tek tek çağırmak zorunda kalmaz.

**Why this priority**: Feature'ın çekirdek değeri budur — dağıtık ürün verisini tek,
hızlı, birleşik bir görünümde sunmak. Tek başına teslim edilse bile kullanılabilir bir
üründür.

**Independent Test**: Var olan bir ürün için vitrin görünümü istenir; yanıtın 3
kaynağın da alanlarını (ad/görsel, stok durumu, indirim oranı) içerdiği tek çağrıda
doğrulanır.

**Acceptance Scenarios**:

1. **Given** bir ürün ve ilgili stok/indirim verisi mevcutken, **When** ürün vitrin
   görünümü istenir, **Then** tüm alanlar tek yanıtta birleşik döner.
2. **Given** bir kaynak alanı henüz raporlanmamışken (kısmi satır), **When** görünüm
   istenir, **Then** eksik alanlar için tutarlı bir "henüz bilinmiyor" değeri döner.
3. **Given** kimlik doğrulanmamış bir istek, **When** vitrin görünümü istenir, **Then**
   istek başarıyla döner (görünüm herkese açıktır, reddedilmez).

---

### User Story 2 - Kaynak veri değişince görünüm güncel yansıtır (Priority: P2)

Bir kaynak servis (Catalog/Stock/Discount) sahibi olduğu veriyi değiştirdiğinde, o
değişiklik ilgili ürünün vitrin görünümüne kısa sürede yansır; görünüm bayat kalmaz.

**Why this priority**: Materialize edilmiş görünümün değeri güncelliğine bağlıdır; ama
US1 çekirdek okumayı tek başına sağladığından, tazelik ikinci dilim olarak eklenir.

**Independent Test**: Bir ürün görünümü okunur; ardından bir kaynak alanı (ör. stok,
indirim oranı) değiştirilir; sonraki okuma yeni değeri yansıtır.

**Acceptance Scenarios**:

1. **Given** görünümde ürün "stokta" iken, **When** stok tükenir, **Then** sonraki
   okuma ürünü "tükendi" olarak yansıtır.
2. **Given** ürüne indirim tanımlıyken, **When** indirim kaldırılır, **Then** sonraki
   okuma indirimsiz döner.
3. **Given** bir kaynak değişikliği iki kez yayınlanırsa (tekrar), **When** görünüm
   güncellenir, **Then** sonuç tek uygulamayla aynıdır (idempotent; mükerrer yok).

---

### User Story 3 - Ürüne özel indirim tanımlanabilir (Priority: P2)

Bir yönetici, belirli bir ürüne bir indirim oranı tanımlayabilir veya kaldırabilir;
bu Discount context'in kendi sorumluluğudur ve tanımlanan/kaldırılan indirim, ürünün
vitrin görünümüne yansır.

**Why this priority**: US1'in "indirim oranı" alanının var olabilmesi için önce bir
yerde tanımlanmış olması gerekir; bugünkü Discount.Api bunu (kullanıcı-bazlı ödül kuponu
olarak) desteklemiyor — bu hikaye o boşluğu kapatır. US1/US2'ye bağımlıdır ama ayrı
test edilebilir bir dilimdir.

**Independent Test**: Bir ürüne indirim oranı tanımlanır; Discount context'in kendi
sorgusundan doğrulanır; ardından Storefront'un vitrin görünümünde de göründüğü
doğrulanır (US2 ile birleşik).

**Acceptance Scenarios**:

1. **Given** bir ürünün indirimi yokken, **When** ürüne bir indirim oranı tanımlanır,
   **Then** Discount context bunu üründe kayıtlı olarak döner.
2. **Given** bir ürüne indirim tanımlıyken, **When** aynı ürüne yeniden bir oran
   tanımlanır, **Then** önceki oranın üzerine yazılır (üründe en fazla bir aktif oran
   olur).
3. **Given** bir ürüne indirim tanımlıyken, **When** indirim kaldırılır, **Then**
   üründe artık aktif bir indirim oranı yoktur.

---

### Edge Cases

- **Kısmi satır**: Bir ürün oluşup henüz stok/indirim raporlanmadıysa görünüm eksik
  alanlarla tutarlı döner; hata fırlatmaz.
- **Sırasız event**: Kaynak event'leri farklı sırayla gelebilir; her alan kaynağına
  göre bağımsız güncellenir, geç gelen eski bir event güncel değeri ezmemelidir.
- **Mükerrer event**: Aynı değişiklik iki kez gelirse sonuç değişmez (idempotent
  upsert).
- **Silinmiş ürün**: Görünümde bir ürün silinmişse (soft-delete) o durum kaynağın son
  bildirdiği haliyle yansır; görünüm otoriter karar üretmez.
- **Bootstrap**: Görünüm ilk ayağa kalktığında geçmiş veri için başlangıç doldurması
  gerekir; event'ler yalnız bundan sonraki değişimi taşır.
- **Var olmayan ürün için indirim**: Silinmiş/var olmayan bir ProductId'ye indirim
  tanımlama girişimi reddedilir (Discount context kendi tarafında doğrular).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, bir ürün için Catalog/Stock/Discount kaynaklarından gelen
  alanları birleştiren tek bir ürün-vitrin görünümü MUST sunsun.
- **FR-002**: Ürün vitrin görünümü MUST tek bir okuma isteğiyle dönsün; istemci ayrı
  servisleri tek tek çağırmasın.
- **FR-003**: Görünüm MUST materialize edilsin (önceden birleştirilip saklansın);
  okuma anında 3 kaynağa senkron çağrı yapılmasın.
- **FR-004**: Görünüm, her kaynağın yayınladığı "değişti" bildirimiyle MUST
  güncellensin; veriyi değiştiren servis (yazan) değişikliği yayınlar
  (writer-publishes).
- **FR-005**: Her alanın MUST tek bir otoriter sahibi olsun; görünüm otoriter veri
  üretmez, yalnızca kaynakların kopyalarını birleştirir.
- **FR-006**: Görünüm güncellemesi MUST idempotent olsun; aynı değişikliğin tekrarı
  sonucu değiştirmesin. Geç gelen eski bir bildirim güncel değeri ezmesin.
- **FR-007**: Görünüme erişim MUST kimlik doğrulaması gerektirmesin; herkese açıktır
  (US1, Senaryo 3).
- **FR-008**: Bir kaynak alanı henüz raporlanmadıysa görünüm MUST kısmi (eksik alanlı)
  ama tutarlı dönsün; eksik veri hata sebebi olmasın.
- **FR-009**: Görünüm, kendi verisini kendi sınırında (kendi deposunda) MUST tutsun;
  başka servisin deposuna doğrudan erişmesin ve deposunu başka servisle paylaşmasın.
- **FR-010**: İlk teslimat kapsamı MUST yalnızca ürün-vitrin görünümü olsun; sipariş ve
  ödeme bu feature'ın kapsamı dışındadır; aynı desenle başka composite görünümler
  sonradan eklenebilir.
- **FR-011**: Görünümü sağlayan çözüm, ilk ayağa kalkışta mevcut ürünler için
  başlangıç doldurması (bootstrap) MUST sağlasın; yalnızca yeni değişikliklere bağlı
  kalmasın.
- **FR-012**: Discount context, bir ürüne indirim oranı tanımlama/güncelleme/kaldırma
  yeteneğini MUST sunsun (bugünkü kullanıcı-bazlı ödül-kuponu modelinden ürün-bazlı
  modele dönüşüm); bir üründe MUST en fazla bir aktif indirim oranı bulunsun.
- **FR-013**: Var olmayan/silinmiş bir ürüne indirim tanımlama girişimi MUST reddedilsin.

### Key Entities *(include if feature involves data)*

- **Ürün Vitrin Görünümü (Product Storefront View)**: Bir ürünün 3 kaynaktan
  birleştirilmiş, denormalize, yassı kopyası. Nitelikler: ürün adı+görsel (Catalog),
  stok durumu (Stock), indirim oranı (Discount). Zengin aggregate değildir.
- **Alan Sahipliği (Field Ownership)**: Her görünüm alanının tek bir kaynak context'e
  ait olduğu eşleme. Görünüm bu eşlemenin dışında otoriter veri üretmez.
- **Değişiklik Bildirimi (Change Notification)**: Bir kaynak servisin sahibi olduğu
  veriyi değiştirdiğinde yayınladığı, görünümün güncellemesini tetikleyen olay. Kaynak
  başına ayrı.
- **Ürün İndirimi (Product Discount)**: Discount context'in ürün-bazlı yeni modeli —
  bir ProductId'ye tanımlanan tek bir aktif indirim oranı. Bugünkü kullanıcı-bazlı ödül
  kuponu modelinin yerini alır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Bir ürün vitrin görünümü, 3 kaynağın alanlarını içerecek şekilde **tek**
  okuma isteğiyle döner (istemci tarafında ek servis çağrısı = 0).
- **SC-002**: Bir kaynak veri değişikliğinin ardından ilgili görünüm en geç 5 saniye
  içinde güncel değeri yansıtır.
- **SC-003**: Aynı değişiklik bildirimi 100 kez tekrarlandığında görünüm sonucu, tek
  uygulamayla birebir aynıdır (idempotency doğrulanır).
- **SC-004**: Bir veya birden çok kaynak alanı henüz raporlanmamışken bile görünüm
  isteği %100 tutarlı (hatasız, kısmi) yanıt döndürür.
- **SC-005**: Kimlik doğrulanmamış istekler dahil, geçerli bir ProductId için görünüm
  istekleri %100 başarıyla döner (erişim reddi = 0).
- **SC-006**: Bir üründe aynı anda birden fazla aktif indirim oranı **hiçbir zaman**
  oluşmaz (en fazla 1, %100 tutarlılık).

## Assumptions

- **Yaklaşım**: Görünüm, servisler-arası **integration event**'leriyle beslenen
  materialize edilmiş bir okuma modelidir; okuma anında senkron kaynak çağrısı
  yapılmaz.
- **Beslenme yönü**: Kaynak servisler görünümün varlığını bilmez; yalnızca kendi
  değişiklik olaylarını yayınlar. Görünüm downstream/conformist'tir, otorite değildir.
- **Bildirim içeriği**: Event'ler fat (self-contained) — Storefront event'i aldıktan
  sonra kaynağa geri dönüp ek veri çekmez; dayanıklı yayın mevcut Wolverine+Marten
  outbox entegrasyonuyla sağlanır (plan aşamasında detaylandırılır).
- **Tazelik modeli**: Anlık tazelik hedeflenir (event-tetikli); kısa süreli, kaynaklar
  arası eventual consistency kabul edilir. TTL/polling birincil mekanizma değildir.
- **Sınır**: Görünüm kendi deposuna sahiptir; paylaşılan veritabanı yoktur;
  cross-service iletişim yalnızca integration event ile olur (anayasa madde I).
  Bootstrap için tek istisna: ilk açılışta MCP üzerinden bir kerelik senkron çekim.
- **Kapsam sınırı**: İlk sürüm yalnızca ürün-vitrin görünümünü kapsar; sipariş, ödeme
  ve kullanıcıya-özel başka görünümler ertelenmiştir. Sipariş/ödeme kapsamı gerekirse
  ayrı bir feature olarak ele alınır.
- **Erişim**: Görünüm herkese açıktır; mevcut kimlik/yetki altyapısı (Identity.Server)
  bu görünüm için zorunlu değildir.
- **Discount dönüşümü**: Discount.Api'nin kullanıcı-bazlı ödül-kuponu aggregate'i,
  ürün-bazlı indirime dönüştürülür. Bu, mevcut kullanıcı-kupon davranışının (sipariş
  sonrası otomatik üretim) kaldırılması ve yerine ürün-bazlı bir modelin gelmesi
  anlamına gelir — geriye dönük uyumluluk hedeflenmez (mevcut kuponlar/akış bu feature
  ile birlikte yerini alır).
- **Bağımlılık**: Kaynak servislerin ilgili "değişti" olaylarını yayınlaması gerekir;
  bazıları yeni olarak eklenecektir. Mevcut mesajlaşma altyapısı yeniden kullanılır.
- **Caching ilişkisi**: Görünüm okumaları sonradan ayrı `002-aop-query-caching`
  feature'ı ile cache'lenebilir; bu feature caching mekanizmasını kapsamaz.