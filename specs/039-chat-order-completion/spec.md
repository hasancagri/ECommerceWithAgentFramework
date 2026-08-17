# Feature Specification: Chat Üzerinden Uçtan Uca Sipariş Tamamlama

**Feature Branch**: `039-chat-order-completion`

**Created**: 2026-08-17

**Status**: Draft

**Input**: User description: "Chat üzerinden uçtan uca sipariş tamamlama (039). Bugün chat A2A ile
kayıtlı karttan taksitli çekim yapıyor (038) ama sipariş/stok chat kapsamı dışı. Kullanıcı chat'te
ödemeyi onayladıktan/başarılı çekimden sonra agent bir Order.Api MCP tool'u ile sipariş oluşturur,
sipariş oluşmadan önce ödeme PaymentGateway'de doğrulanır ve mevcut checkout saga (028) tetiklenir;
sepet kalemleri sunucu tarafından sentezlenir, LLM'e girmez."

**Artefakt kademesi**: **Tam** — yeni endpoint kontratı (Order agent slice + `place_order` MCP tool),
yeni servisler-arası dikiş (Order→PaymentGateway verify REST, Order→Basket kalem okuma) ve para-kritik
doğrulama tasarımı var.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Chat'te siparişi tamamla (Priority: P1)

Giriş yapmış müşteri sepetini doldurmuş, chat'te kayıtlı kartıyla ödemeyi onaylamıştır (038). Başarılı
çekimden sonra kullanıcı "siparişimi tamamla" der; sistem ödemeyi PaymentGateway'de doğrular, siparişi
oluşturur, stok kalıcı düşer, sepet temizlenir ve kullanıcıya sipariş numarasını bildirir — hiçbir
ekrana gitmeden.

**Why this priority**: Feature'ın çekirdek değeri. Tek başına teslim edilirse "chat'ten sipariş"
vaadini karşılar.

**Independent Test**: Sepette ürün + başarılı çekim (paymentId) varken chat'te "siparişi tamamla"
denir; ödeme doğrulanır, bir Confirmed sipariş oluşur, stok düşer, sepet boşalır.

**Acceptance Scenarios**:

1. **Given** sepette 2 kalem + PaymentGateway'de başarılı çekim, **When** kullanıcı chat'te siparişi
   onaylar, **Then** ödeme PG'de doğrulanır, Pending sipariş oluşur, saga çalışır, sipariş Confirmed
   olur ve sepet boşalır.
2. **Given** sipariş Confirmed oldu, **When** agent yanıtı döner, **Then** kullanıcıya sipariş kodu
   ve özet (kalem sayısı, tutar) chat'te bildirilir.
3. **Given** siparişin alıcı/adres bağlamı, **When** sipariş oluşturulur, **Then** alıcı ve adres
   `get_payment_context`'ten gelen değerlerle birebir (VERBATIM) kaydedilir; LLM üretmez/değiştirmez.
4. **Given** sepet kalemleri, **When** sipariş oluşturulur, **Then** kalemler (ürün, fiyat, adet)
   sunucu tarafından kullanıcının sepetinden sentezlenir; LLM'e verilmez ve LLM'den alınmaz.

---

### User Story 2 - Ödeme doğrulaması (para-kritik geçit) (Priority: P1)

Sipariş oluşturmadan önce sistem, gelen paymentId'yi PaymentGateway'de **sunucu-sunucu** doğrular:
ödeme gerçekten başarılı mı, tutar sepetle uyuşuyor mu, ödeme çağıran kullanıcıya mı ait. Bu doğrulama
LLM'den bağımsızdır; LLM'in "ödeme başarılı" demesi tek başına sipariş açtırmaz.

**Why this priority**: paymentId varlığı başarı kanıtı değildir; başarısız/pending çekim de id taşır.
Ayrıca id zinciri chat'te LLM'den geçer — uydurma/enjeksiyon bir "başarı" bedava sipariş bastırabilir.
Para bütünlüğü için P1.

**Independent Test**: (a) Başarısız/pending bir paymentId verilir → sipariş oluşmaz. (b) Tutarı
sepetle uyuşmayan ödeme → red. (c) Başka kullanıcının ödemesi → red.

**Acceptance Scenarios**:

1. **Given** paymentId PG'de `status != success`, **When** sipariş talep edilir, **Then** sipariş
   oluşturulmaz; kullanıcıya ödeme doğrulanamadığı bildirilir.
2. **Given** PG'deki ödeme tutarı sunucu-sentezli sepet toplamıyla uyuşmuyor, **When** sipariş talep
   edilir, **Then** sipariş oluşturulmaz.
3. **Given** paymentId çağıran kullanıcıya ait değil, **When** sipariş talep edilir, **Then** sipariş
   oluşturulmaz (başkasının ödemesiyle sipariş açılamaz).
4. **Given** doğrulama başarılı (`status==success` AND `tutar==sepet` AND `sahip==çağıran`), **When**
   sipariş talep edilir, **Then** sipariş oluşturulur.

---

### User Story 3 - İdempotent tekrar deneme — çift çekim/sipariş yok (dayanıklılık) (Priority: P1)

Çekim başarılı (para alınmış) fakat sipariş oluşturma anlık bir hatayla tamamlanamamıştır. Sistem
parayı "sessizce yutmamalı": aynı ödeme için yeniden denenebilmeli, tekrar denemede **çift çekim
(correlation-key) veya çift sipariş (paymentId)** üretilmemelidir.

**Why this priority**: Charge-önce/sipariş-sonra sıralaması bir tutarsızlık penceresi açar; para
alınıp sipariş açılmaması kabul edilemez.

**Independent Test**: Sipariş oluşturma bir kez başarısız edilir; kullanıcı tekrar dener; aynı
paymentId için tek sipariş oluştuğu (idempotent) doğrulanabilir.

**Acceptance Scenarios**:

1. **Given** başarılı çekim ama sipariş oluşturma geçici hata verdi, **When** kullanıcı tekrar
   "siparişi tamamla" der, **Then** aynı paymentId için yeni sipariş oluşmaz; var olan/yeni tek
   sipariş döner — çift sipariş / çift stok düşümü olmaz.
2. **Given** çekim başarılı ama sipariş henüz açılamadı, **When** agent yanıt verir, **Then**
   kullanıcıya ödemenin alındığı ama siparişin tamamlanamadığı ve tekrar deneyebileceği açıkça
   bildirilir (para kaybı algısı oluşmaz).

---

### User Story 4 - Çekim başarılı ama yanıt kayıp (kurtarma) (Priority: P1)

Kullanıcı ödemeyi onaylamış, PaymentGateway çekimi başarıyla yapmış; fakat yanıt bize dönmeden
bağlantı kopmuştur — elimizde **paymentId bile yoktur**. Sistem, kullanıcı tekrar denediğinde
**çift çekim yapmamalı** ve alınmış ödemeyi **kurtarıp** siparişe dönüştürebilmelidir. Para asla
sessizce yetim kalmaz.

**Why this priority**: En sert tutarsızlık penceresi — para alınmış, id yok. Çift çekim ve yetim para
doğrudan güven ve para bütünlüğünü bozar. P1.

**Independent Test**: Çekim başarılı edilir ama yanıt "kaybedilir" (elde id yok); kullanıcı tekrar
dener → yeni çekim yapılmaz (dedupe), alınmış ödeme korelasyon anahtarıyla bulunur, doğrulanır ve
tek sipariş oluşur.

**Acceptance Scenarios**:

1. **Given** çekim öncesi sunucu bir korelasyon anahtarı ürettmiş (LLM değil), **When** çekim yanıtı
   kaybolur ve kullanıcı tekrar öder/dener, **Then** PaymentGateway aynı anahtarı gördüğü için yeni
   çekim yapmaz; var olan başarılı ödemeyi döner (çift çekim yok).
2. **Given** yanıt kaybı sonrası elde paymentId yok, **When** sistem toparlanır, **Then** ödeme
   korelasyon anahtarıyla PaymentGateway'de sorgulanır, başarılıysa doğrulanır ve sipariş oluşturulur.
3. **Given** çekim sonucu belirsiz (yanıt kayıp, henüz teyit edilmedi), **When** agent yanıt verir,
   **Then** kullanıcıya asla kesin "başarısız" denmez; "ödemen alınmış olabilir, kontrol ediyoruz /
   tekrar dene" gibi para-kaybı algısı oluşturmayan mesaj verilir.

---

### User Story 5 - Chat'te sipariş durumunu gör (Priority: P3)

Kullanıcı siparişi tamamladıktan sonra chat'te "siparişlerim" / "son siparişim ne oldu" diye sorar ve
durumu görebilir.

**Why this priority**: Kapanış ve güven verir; ancak mevcut `get_orders` okuma tool'u zaten karşılar.
Yeni yetenek değil, akışa bağlanmasıdır. P3.

**Independent Test**: Sipariş oluştuktan sonra "siparişlerim" sorulur; yeni siparişin listede
göründüğü doğrulanabilir.

**Acceptance Scenarios**:

1. **Given** yeni oluşmuş sipariş, **When** kullanıcı chat'te siparişlerini sorar, **Then** sipariş
   kodu ve güncel durumuyla listelenir.

---

### Edge Cases

- **Boş sepet**: Sepet boşken sipariş talebi gelirse oluşturulmaz; kullanıcıya sepet gerektiği bildirilir.
- **Ödeme yok**: Geçerli bir paymentId olmadan sipariş talebi reddedilir — sipariş daima bir ödemeye bağlıdır.
- **Adres bağlamı yok**: `get_payment_context` adres döndürmezse (NotFound) sipariş oluşturulmaz; adres
  eksik bildirilir (038 NotFound davranışıyla tutarlı).
- **PG erişilemez (verify)**: Doğrulama yapılamıyorsa sipariş oluşturulmaz (fail-closed); kullanıcıya
  tekrar denemesi söylenir. Ödeme zaten alınmış olabilir → US3 mesajı geçerli.
- **Stok pivot sonrası hata**: Saga Confirm sonrası sepet temizleme başarısız olursa sipariş İPTAL
  EDİLMEZ (028 pivot kuralı); temizleme retryable adımdır.
- **Stok yetersiz (commit anında)**: Saga telafi eder (RevertCommit + Cancel); kullanıcıya sipariş
  tamamlanamadığı bildirilir. (Ödeme iadesi bu feature kapsamı dışı — bkz. Assumptions.)
- **Kart ekleme/silme talebi**: Chat'ten kart yönetimi YAPILMAZ (güvenlik); yalnız ekran yolu.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, chat'te başarılı çekim (paymentId) sonrası kullanıcının doğal-dil onayıyla
  ("siparişi tamamla" vb.) sipariş oluşturmayı tetikleyebilen bir **agent yüzeyi** sağlamalıdır.
- **FR-002**: Sipariş oluşturulmadan ÖNCE sistem, ödemeyi PaymentGateway'de **sunucu-sunucu
  (LLM'den bağımsız)** doğrulamalıdır. Doğrulama üç koşulu birlikte gerektirir: (a) ödeme durumu
  başarılı, (b) ödeme tutarı sunucu-sentezli sepet toplamıyla uyumlu, (c) ödeme çağıran kullanıcıya
  ait. Sahiplik (c), retrieve'in **yalnız çağıranın userId'sinden türetilen HMAC correlation-key ile**
  yapılmasıyla sağlanır (başka kullanıcı anahtarı üretemez). Üçü sağlanmadıkça sipariş oluşturulmaz.
- **FR-003**: Doğrulama başarılıysa sistem, mevcut checkout saga'yı (028) tetiklemelidir: kalem kalem
  stok commit, sipariş Confirmed, ardından sepet temizleme.
- **FR-004**: Siparişin kalemleri (ürün, birim fiyat, adet) kullanıcının sepetinden **sunucu
  tarafından** sentezlenmelidir; LLM'e verilmemeli ve LLM'den alınmamalıdır.
- **FR-005**: Siparişin alıcı ve adres bilgisi `get_payment_context`'ten gelen değerlerle **birebir
  (VERBATIM)** kaydedilmelidir; agent bu değerleri üretmemeli/değiştirmemelidir.
- **FR-006**: Her sipariş, doğrulanmış çekimin `paymentId`'sine bağlanmalıdır; ödemesiz veya
  doğrulanmamış ödemeyle sipariş oluşturulamaz.
- **FR-007**: Sipariş oluşturma `paymentId` bazında **idempotent** olmalıdır: aynı ödeme için tekrar
  tetikleme çift sipariş / çift stok düşümü üretmemelidir.
- **FR-008**: **Doğrulanmış çekim BAŞARILI** olduğu halde (çekim kesin) **sipariş oluşturma adımı**
  anlık hatayla tamamlanamazsa, sistem bu adımı **idempotent yeniden denemeli** ve kullanıcıya siparişin
  tamamlanmakta olduğu/tekrar denenebileceği bildirilmelidir; çift sipariş oluşmaz. (Bu, çekim sonucunun
  **belirsiz** olduğu durumdan ayrıdır — o FR-021/US4.)
- **FR-009**: Sipariş talebi geldiğinde sepet boşsa, geçerli çekim yoksa veya PG doğrulaması
  başarısızsa sistem siparişi oluşturmamalı, eksik/başarısız koşulu kullanıcıya bildirmelidir.
- **FR-010**: Chat sipariş yüzeyi yalnız giriş yapmış kullanıcı için çalışmalı ve kullanıcının kendi
  yetkisiyle (token) sipariş oluşturmalıdır; başka kullanıcı adına sipariş açılamaz.
- **FR-011**: Sipariş oluşturulduğunda kullanıcıya chat'te sipariş kodu ve kısa özet (kalem
  sayısı/tutar) bildirilmelidir.
- **FR-012**: Kullanıcı chat'te siparişlerini/son siparişini sorabilmeli ve güncel durumunu
  görebilmelidir (mevcut okuma yüzeyi üzerinden).
- **FR-013**: Chat akışından kart ekleme/silme YAPILAMAZ.
- **FR-014**: PG doğrulama çağrısı yapısal (LLM'siz) sunucu-sunucu bir kanaldır ve merchant/servis
  kimliğiyle (kullanıcı JWT'si değil) kimliklenir; para-güveni hiçbir zaman LLM üzerinden taşınmaz.
- **FR-015**: Çekim başlatılmadan önce sistem, **sunucu tarafından (LLM değil)** bir korelasyon/
  idempotency anahtarı üretmeli (kullanıcı + sepet + deneme bazlı) ve çekim isteğine koymalıdır.
  Bu anahtar LLM tarafından üretilmez/değiştirilmez.
- **FR-016**: Çekim, korelasyon anahtarı bazında **idempotent** olmalıdır: aynı anahtarla tekrar
  çekim talebi yeni tahsilat yapmaz, var olan ödemeyi döner (çift çekim yok).
- **FR-017**: Çekim yanıtı belirsiz/kayıpken (elde paymentId yok) sistem, ödemeyi **korelasyon
  anahtarıyla** PaymentGateway'de sorgulayabilmelidir.
- **FR-018**: Sistem, belirsiz kalan ödemeleri **durable ve sınırlı** bir reconcile döngüsüyle tekrar
  tekrar kontrol etmelidir: her denemede PG'ye anahtarla sorar; `success` → doğrula + sipariş, `failed`
  → işaretle, `pending`/erişilemez → **backoff ile yeniden zamanla**, bir **deadline**'a kadar.
- **FR-019**: Reconcile hem **arka planda (durable scheduled)** hem **ön planda (kullanıcı chat'te
  tekrar sorunca hemen)** tetiklenebilmeli; ikisi de aynı idempotent `reconcile(correlationKey)`
  işlemine inmelidir. Döngü kaç kez koşarsa koşsun tek çekim + tek sipariş üretir.
- **FR-020**: Deadline dolmasına rağmen çözülemeyen ödeme **terminal `needs-reconciliation`** durumuna
  geçer; sessizce düşürülmez — kullanıcıya "kontrol ediliyor" bildirilir ve operasyonel görünürlük
  (log/kuyruk) bırakılır. Reconcile döngüsü **asla sonsuz** değildir.
- **FR-021**: Çekim sonucu belirsizken kullanıcıya kesin "başarısız" bildirilmez; ödemenin alınmış
  olabileceği ve durumun kontrol edildiği/yeniden denenebileceği bilgisi verilir.

### Key Entities *(include if data involved)*

- **Sipariş (Order)**: Kullanıcıya ait, doğrulanmış bir ödemeye (paymentId) ve bir teslim adresine
  bağlı, kalem listesi taşıyan; Pending doğup saga sonunda Confirmed/Cancelled olan aggregate.
- **Sipariş Kalemi (OrderItem)**: Sepetten sentezlenen ürün+birim fiyat+adet; siparişe ait entity.
- **Ödeme Bağlamı (Payment Context)**: Çekim için kullanılan/varsayılan kartın alıcı + varsayılan
  adres bilgisi; `get_payment_context`'ten VERBATIM alınır.
- **Ödeme Doğrulama Sonucu (Payment Verification)**: PaymentGateway'den çekilen kayıt: durum
  (başarılı/başarısız/pending), tutar (temel + tahsil edilen), para birimi, sahip (buyer) referansı,
  sağlayıcı ödeme kimliği. Sipariş açma geçidinin girdisidir.
- **Ödeme Denemesi (Payment Attempt)**: Bir çekim girişiminin dayanıklılık kaydı: korelasyon anahtarı,
  kullanıcı, sepet-snapshot referansı, durum (`charging`/`unknown`/`success`/`failed`/
  `needs-reconciliation`), deneme sayısı, sonraki kontrol zamanı. Reconcile döngüsünün sahibi; sipariş
  bundan doğar. Korelasyon anahtarı çekim idempotency'sini ve kayıp-yanıt kurtarmayı taşır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Giriş yapmış kullanıcı, sepeti dolu ve ödemesi onaylı iken hiçbir ekrana gitmeden, yalnız
  chat üzerinden siparişini tamamlayabilir (Confirmed sipariş + boşalmış sepet).
- **SC-002**: Sipariş oluştuğunda kullanıcı, chat yanıtında sipariş kodunu ve özetini görür.
- **SC-003**: Doğrulaması başarısız (başarısız/pending/tutar uyuşmaz/başka kullanıcı) ödemelerin
  **%100'ünde** sipariş oluşmaz.
- **SC-004**: Aynı ödeme için sipariş oluşturma birden çok tetiklense de en fazla **bir** sipariş
  oluşur (çift sipariş oranı %0).
- **SC-005**: Çekim başarılı ama sipariş oluşturulamayan durumların **%100'ünde** kullanıcı, para kaybı
  algısı oluşturmayan, tekrar deneme yönlendirmesi içeren bir mesaj alır.
- **SC-006**: Sipariş kalemleri, alıcı/adres ve ödeme-güveni hiçbir durumda LLM tarafından üretilmez;
  kalemler sepetten, alıcı/adres `get_payment_context`'ten, ödeme onayı PG doğrulamasından gelir
  (denetimle doğrulanabilir).

## Dependencies

### Dış Bağımlılık — PaymentGateway verify yüzeyi (ayrı repo)

Bu feature'ın para-kritik geçidi (FR-002), PaymentGateway'in bir **retrieve-by-id** yüzeyi açmasına
bağlıdır. Bu iş PaymentGateway repo'sunda yapılır ve 039'un dışındadır; 039 yalnız tüketir.

- **Durum**: PaymentGateway çekimleri zaten kalıcı saklıyor (`Payment` kaydı: sağlayıcı ödeme kimliği,
  tutar temel+tahsil, durum; başarı ve başarısız ikisi de yazılıyor). **Eksik olan tek şey okuma
  yüzeyidir** — bugün by-id sorgu/endpoint yok (write-only).
- **PG'nin sağlaması gereken (verify)**: paymentId **veya korelasyon anahtarı** ile korunan
  (merchant/servis kimliği) bir okuma ucu; yanıtta **zorunlu**: `status` (başarılı/başarısız/pending),
  `price` (temel tutar), `paidPrice` (tahsil edilen), `currency`, sahip (buyer) referansı, sağlayıcı
  ödeme kimliği. **Faydalı**: taksit sayısı, sağlayıcı işlem kimliği (iade için), maskeli kart,
  oluşturulma zamanı.
- **PG'nin sağlaması gereken (idempotent çekim)**: Çekim, çağıranın verdiği **korelasyon anahtarı**
  bazında idempotent olmalı — aynı anahtarla tekrar çekim yeni tahsilat yapmaz, var olan ödemeyi
  döner (FR-016). Ödeme kaydı bu anahtarla da sorgulanabilmeli (kayıp-yanıt kurtarma, FR-017).
- **Sahiplik — kapandı** (bkz. yukarıdaki "Sahiplik (FR-002c) — ÇÖZÜLDÜ"): PG'ye ayrı buyer referansı
  eklenmez; sahiplik, caller-türetimli **HMAC correlation-key** ile sağlanır (anahtar userId içerir,
  forge edilemez, retrieve yalnız çağıranın hesapladığı anahtarla). PG yalnızca anahtarı persist+indeks
  eder (zaten idempotency/reconcile için gerekli).
- **PG-tarafı iş özeti (039 dışında, ayrı repo — Yol 2 ile genişledi)**: (a) **yapısal çekim ucu**
  (server-to-server REST; vaultToken + buyer + amount + **correlation-key** alır), (b) çekimde
  korelasyon anahtarı **persist + indeksle** + **idempotent dedupe**, (c) **retrieve-by-anahtar** (ve
  by-id) okuma ucu (verify + reconcile için). 039 bu yüzeyleri yalnız tüketir; PG-tarafı implementasyon
  paralel bir iş kalemidir. **Not:** bugünkü A2A charge skill'i, chat-sipariş akışında yapısal çekimle
  değişir; A2A taksit-sorgu skill'i kalabilir. PG bugün buyer/correlation persist ETMİYOR (yalnız
  MerchantId+VaultToken+tutar+status) → correlation-key persist bu değişikliğe dahildir.
- **Sahiplik (FR-002c) — ÇÖZÜLDÜ, ayrı buyer alanı GEREKMEZ**: Correlation-key `userId`'yi içerir ve
  **HMAC** (sunucu secret'ı) ile üretilir → forge edilemez. Order.Api ödemeyi yalnız **çağıranın
  userId'sinden yeniden hesapladığı** anahtarla retrieve eder; başka kullanıcı bu anahtarı üretemez →
  başkasının ödemesini göremez/kullanamaz. Ownership anahtarın kendisindedir. Ek kat: vaultToken zaten
  yalnız caller'ın kartlarından gelir (Customer context). PG'ye ayrı buyer referansı eklemek gerekmez.

### Yeni iç kontratlar (bu repo — Yol 2)

- **Order.Api → Customer.Api (yapısal)**: ödeme bağlamı (buyer + vaultToken + varsayılan adres) —
  bugün `get_payment_context` yalnız MCP (agent). Order.Api yapısal (gRPC/REST) tüketim ister
  (İlke I: agent-olmayan kod MCP süremez).
- **Order.Api → Basket (yapısal gRPC)**: sepet kalemlerini okuma — bugün yalnız `ClearBasket` gRPC
  var; **yeni `GetBasketItems` RPC** gerekir (kalem sunucu-sentezi için).
- **Order.Api → PaymentGateway (yapısal REST)**: çekim + verify/retrieve (yukarıdaki PG işi).

### İç Bağımlılıklar (bu repo)

- **028 CheckoutSaga**: Sipariş yaşam döngüsü orkestrasyonu aynen kullanılır.
- **038 chat ödeme akışı**: Başarılı çekim + `get_payment_context` bu feature'ın önkoşuludur.
- **Basket**: Sipariş kalemlerinin sunucu-otoritesi; sepet okuması sunucu tarafında yapılır.

## Assumptions

- **Çekim sunucu orkestrasyonunda (Yol 2)**: 039, çekimi chat'in LLM-A2A adımından çıkarıp
  **sunucu-tarafı durable akışa** taşır. Kullanıcı chat'te tetikler; sunucu (Order.Api) sırayla:
  correlation-key üretir → PaymentAttempt açar → **PG'ye yapısal (REST) çekim** yapar → sipariş
  oluşturur → saga tetikler → belirsizlikte reconcile eder. Taksit **sorgusu** (read-only, para
  taşımaz) mevcut A2A/agent yolunda kalabilir; **çekim** (para hareketi) yapısaldır.
- **Neden yapısal çekim**: Correlation-key'in çekimden önce sunucuda kurulması + durable
  PaymentAttempt/reconcile, stateless ChatAgent'ta ve LLM-güdümlü A2A'da yapılamaz. Anayasa İlke I:
  agent-olmayan kod A2A/MCP süremez → para hareketi yapısal REST/gRPC ile.
- **Agent tetikler, sunucu yürütür**: Kullanıcı yüzeyi/tetikleme agent-merkezlidir (NL → tek Order.Api
  MCP tool seçimi; LLM yalnız kart seçimi + taksit sayısı verir). Para-kritik iş (çekim, kalem sentezi,
  buyer VERBATIM, verify, saga, reconcile) LLM'den bağımsız sunucu kodundadır. MCP tool ince
  sarmalayıcıdır (İlke III); güven/para hiçbir zaman LLM'de taşınmaz.
- **Ödeme iadesi kapsam dışı**: Sipariş saga tarafından iptal edilirse (stok yetersiz vb.) otomatik
  para iadesi bu feature'da YOK; kullanıcı bilgilendirilir, iade ayrı yeteneğe bırakılır.
- **Idempotency anahtarı = paymentId**: Bir başarılı+doğrulanmış çekim en fazla bir siparişe bağlanır.
- **Sepet kalemleri sunucu-otoritesi**: Chat'te sipariş kalemleri kullanıcının canlı sepetinden okunur;
  agent/LLM kalem listesini taşımaz (fiyat/adet manipülasyonu imkansız).
- **Stok güvencesi = münhasır sepet rezervasyonu**: Stok, sepete-eklemede (012) o kullanıcıya münhasır
  rezerve edilir; süre dolunca sepet temizlenir (020/026, geri sayım 025). place_order canlı sepeti
  okuduğundan (GetBasketItems) süresi geçmiş kalem zaten sepette YOKtur → red. Bu yüzden **charge-öncesi
  ayrı stok kontrolü/re-reserve GEREKMEZ**. Artık risk yalnız checkout akışının kendi süresinin TTL'i
  aşması (son-saniye başlatma); geri sayım dakikalar mertebesinde olduğundan bu marjinal kabul edilir.
- **Mevcut saga yeniden kullanılır**: 028 CheckoutSaga adımları/telafisi/pivot kuralı aynen geçerli;
  yeni orchestration açılmaz.
- **Reconcile durable scheduling üstünde**: Belirsiz ödemelerin tekrar-kontrol döngüsü, cron değil,
  mevcut Wolverine durable scheduled-message primitifiyle kurulur (028 watchdog / 026 `ScheduleAsync`
  ile aynı desen); backoff + deadline sınırlıdır, kalıcıdır (süreç yeniden başlasa da yaşar).
- **Korelasyon anahtarı sunucu-üretimi**: Anahtar çekimden önce sunucuda üretilir ve çekim isteğine
  enjekte edilir (038'deki buyer VERBATIM enjeksiyonu gibi); LLM anahtarı seçmez/görmez.
- **Yetki**: Chat sipariş yüzeyi kullanıcının kendi token'ıyla; saga arka plan adımları mevcut makine
  token'ı (order-saga) ile (028). PG verify ise merchant/servis kimliğiyle (yapısal REST).
- **Okuma yüzeyi hazır**: Sipariş görünürlüğü için mevcut `get_orders` MCP tool'u kullanılır.