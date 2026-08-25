# Feature Specification: Checkout Orchestrator (standalone orchestration-based saga)

**Feature Branch**: `049-checkout-orchestrator`

**Created**: 2026-08-25

**Status**: Draft

**Input**: User description: "Checkout.Orchestrator: ayrı standalone orchestration servisi (kendi Postgres DB'si) ile checkout sürecini yönet; Order BC içindeki mevcut CheckoutSaga tamamen sökülür (full replace); broker-only async command/reply; iki-fazlı payment (authorize→capture, void); endüstri-standart dağıtık tutarlılık (compensating transactions, transactional outbox, idempotent consumer, saga log, durable timers, dead-letter). Öğrenme/keşif projesi."

## Bağlam & Amaç *(bilgilendirme)*

Bu bir **öğrenme/keşif** feature'ı — ticari kaygı yok. Amaç: checkout sürecini, mevcut
BC-içi saga yerine, **ayrı bir orchestration servisinde** ve **saf broker haberleşmesiyle**
kurup dağıtık tutarlılığın tam bedelini (idempotency, telafi, eventual consistency, kısmi
başarısızlık) gerçek endüstri desenleriyle yaşamak. Mevcut Order BC içindeki `CheckoutSaga`
**tamamen sökülür** (yan-yana koşum yok; eski tasarım git geçmişinde kalır).

**Checkout'un tanımı (kesin):** Checkout, sepette ürün olması değil, alıcının **"Ödemeyi
Tamamla" (POST)** ile niyeti bağlayıcı sürece çevirdiği andır. Sepete atma = **rezervasyon**
(Stock BC, geçici TTL hold); checkout = **süreç başlangıcı** (saga doğar). İkisi farklı an,
farklı sahip.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Başarılı checkout uçtan uca (Priority: P1)

Kayıtlı adres + kartı olan alıcı "Ödemeyi Tamamla" der; sistem ödemeyi bloke eder
(authorize), her kalem için mevcut rezervasyonu kesinleştirir (commit), ödemeyi tahsil eder
(capture), siparişi onaylar ve sepeti temizler. Alıcı "siparişin alındı" bildirimi alır ve
sipariş Confirmed görünür.

**Why this priority**: Mutlu yol olmadan feature'ın hiçbir değeri yok; diğer akışlar bunun
sapmalarıdır. Tek başına MVP.

**Independent Test**: Geçerli rezervasyonlu + ödeme onaylanabilir bir sepetle POST; sipariş
Confirmed, stok kalıcı düşmüş, ödeme Captured, sepet boş olur.

**Acceptance Scenarios**:

1. **Given** kalemleri geçerli rezervasyonlu ve ödeme onaylanabilir bir sepet, **When** alıcı checkout başlatır, **Then** sipariş Confirmed olur, tüm rezervasyonlar kalıcı düşüşe çevrilir, ödeme Captured olur, sepet boşalır.
2. **Given** checkout başlatıldı, **When** alıcı hemen "Siparişlerim"e bakar, **Then** sipariş süreç tamamlanana kadar Pending, tamamlanınca Confirmed görünür (süreç arka planda ilerler).

---

### User Story 2 - Rezervasyon commit başarısızlığında temiz geri sarma (pivot öncesi) (Priority: P1)

Ödeme bloke edildikten sonra bir kalemin rezervasyon commit'i başarısız olur (TTL yarış
penceresinde düştü / rezervasyon geçersiz / Stock geçici hata). **Bu bir "stok yetersiz"
kontrolü DEĞİLDİR** — müsaitlik sepete atarken kararlaştı; commit yalnız tutulan hold'u
kesinleştirir. Sistem daha önce kesinleşmiş kalemleri geri sarar, ödeme blokesini void eder
(gerçek para tahsil edilmediği için iade yok) ve siparişi iptal eder.

**Why this priority**: Telafi doğruluğu feature'ın var oluş sebebi; para/stok tutarlılığı
bozulursa sistem güvenilmez.

**Independent Test**: Bir kalemin rezervasyonunu commit anından hemen önce düşür; sipariş
Cancelled, kesinleşmiş kalemler geri eklenmiş, ödeme Voided (Captured DEĞİL), sepet
korunmuş olur.

**Acceptance Scenarios**:

1. **Given** iki kalemli sepet, ikinci kalemin rezervasyonu commit anında geçersiz, **When** ilk kalem kesinleşir, **Then** ilk kalem geri sarılır, ödeme void edilir, sipariş Cancelled olur ve iptal sebebi kaydedilir.
2. **Given** telafi sürüyor, **When** aynı geri-sarma komutu (broker redelivery ile) ikinci kez gelir, **Then** stok yalnız bir kez geri eklenir (idempotent), çift geri-sarma olmaz.

---

### User Story 3 - Pivot sonrası: onaylanmış sipariş asla iptal olmaz (Priority: P1)

Ödeme tahsil edilip (capture) sipariş onaylandıktan sonra, süreç geç adımı (sepet temizliği)
başarısız olsa bile sipariş **Confirmed kalır** ve iptal edilmez. Geç adım güvenli biçimde
tekrar denenir; kalıcı başarısızlıkta loglanıp geçilir (log-and-complete).

**Why this priority**: "Para alındı ama sipariş kayboldu" veya "onaylı sipariş sonradan
iptal oldu" en yıkıcı tutarsızlıklar; pivot kuralı bunları imkânsız kılar.

**Independent Test**: Sepet temizleme adımını başarısız olacak şekilde tetikle; sipariş
Confirmed KALIR, süreç tükeninceye kadar tekrar dener, sonunda tamamlanır (iptal yok).

**Acceptance Scenarios**:

1. **Given** ödeme Captured ve sipariş Confirmed, **When** sepet temizliği başarısız olur, **Then** sipariş Confirmed kalır ve sepet temizliği sınırlı kez tekrar denenir.
2. **Given** watchdog süresi pivot sonrası dolar, **When** zaman aşımı tetiklenir, **Then** süreç iptal ETMEZ; yalnızca tamamlanır/loglanır.

---

### User Story 4 - Servis kesintisinde dayanıklılık (broker-only) (Priority: P2)

Bir hedef servis (Stock/Payment/Basket/Order) geçici olarak erişilemezse, orchestration
komutu broker kuyruğunda bekler ve servis dönünce işlenir; orchestrator bloke olmaz. Geçici
hatalar sınırlı kez, artan gecikmeyle (backoff) tekrar denenir; kalıcı zehirli mesaj
dead-letter'a düşer.

**Why this priority**: Broker-only mimarinin asıl kazancı temporal decoupling; bunu
gösteremezsek mimari seçimi haksız kalır.

**Independent Test**: Bir hedef servisi süreç ortasında durdur; orchestrator askıda kalmaz;
servis dönünce süreç kaldığı yerden ilerler ve doğru sonuçla biter.

**Acceptance Scenarios**:

1. **Given** checkout commit adımında, **When** Stock servisi kısa süre erişilemez, **Then** komut kuyrukta bekler, orchestrator başka işi bloke etmez, Stock dönünce adım tamamlanır.
2. **Given** bir adım kalıcı olarak başarısız (retry tükendi), **When** son deneme de düşer, **Then** mesaj dead-letter'a taşınır ve süreç bu hata sınıfının kuralına göre (telafi/iptal veya log-and-complete) davranır.

---

### User Story 5 - Süreç sahibi restart'a dayanır (Priority: P2)

Orchestrator uygulaması sürecin ortasında yeniden başlarsa, saga durumu kalıcı olduğundan
süreç kaldığı adımdan devam eder; hiçbir adım kaybolmaz veya çift işlenmez.

**Why this priority**: Durable saga'nın temel vaadi; restart'ta süreç kaybı öğrenme
hedefinin merkezindeki tutarlılık dersidir.

**Independent Test**: Süreç ortasında orchestrator'ı öldür + yeniden başlat; süreç tamamlanır
ve nihai durum (Confirmed/Cancelled) tek ve doğru olur.

**Acceptance Scenarios**:

1. **Given** checkout capture adımında, **When** orchestrator restart olur, **Then** süreç kaldığı yerden devam eder ve sipariş doğru nihai duruma ulaşır.
2. **Given** restart sonrası bekleyen komutlar yeniden teslim edilir, **When** saga onları işler, **Then** durum makinesi faz-guard + idempotency sayesinde tekrar-işlemede bozulmaz.

---

### Edge Cases

- **Rezervasyon TTL'i checkout ÖNCESİ dolmuş:** POST giriş guard'ında yakalanır (mevcut `Order/Create` sayfası `IsReservationExpired`), alıcı sepete döndürülür. Saga hiç doğmaz, auth alınmaz → geri sarılacak bir şey yok.
- **Rezervasyon TTL'i saga İÇİNDE dolmuş (yarış penceresi):** Guard geçti ama commit anında düştü. TTL yapısal olarak **daima pivot öncesindedir** (capture yalnız tüm kalemler commit olunca gelir), dolayısıyla daima temiz geri sarma + void + iptal (asla refund). Bkz. US2.
- **Çift checkout (aynı sepet iki kez POST):** Aynı süreç iki kez başlatılamaz (idempotent başlatma anahtarı); ikinci istek yeni paralel süreç doğurmaz.
- **Authorize başarısız (yetersiz limit):** Süreç en baştan durur; hiç kalem kesinleşmez, sipariş Cancelled (veya hiç oluşturulmaz — bkz. FR-004/FR-014).
- **Capture başarısız (nadir, authorize sonrası):** Kesinleşmiş kalemler geri sarılır, auth void edilir, sipariş Cancelled — capture henüz pivot'u tamamlamadığı için iptal serbest.
- **Bir adımın yanıtı hiç gelmez:** Per-step timeout devreye girer; adım hata sınıfına göre retry veya telafiye yönlendirilir.
- **Tamamlanmış sürece geç mesaj:** Sessizce düşürülür (no-op), yan etki üretmez.
- **Aynı yanıt event'i iki kez (broker at-least-once):** Faz-guard + idempotency ile yalnız bir kez etki eder.

## Requirements *(mandatory)*

### Functional Requirements

**Süreç sahipliği & giriş**

- **FR-001**: Sistem, checkout sürecini Order BC dışında, kendi kalıcı durum deposuna sahip **ayrı bir orchestration bileşeni** üzerinden yürütmelidir.
- **FR-002**: Mevcut Order BC-içi checkout saga'sı **tamamen kaldırılmalıdır**; iki süreç aynı anda koşmamalıdır.
- **FR-003**: Checkout'un giriş noktası orchestration bileşeni olmalıdır; alıcının "Ödemeyi Tamamla" isteği bu bileşene ulaşır ve süreç orada doğar.
- **FR-004**: Sipariş kaydının oluşturulması sürecin **ilk adımı** olmalıdır (Order'a uzaktan komut); sipariş, süreç boyunca Pending doğar.

**Aggregate davranışının korunması**

- **FR-005**: Order/Stock/Basket/Payment kendi domain davranışlarını (durum geçişleri, invariant'lar) **korumalıdır**; orchestrator bu davranışları uzaktan komutla tetikler, kendi içinde tekrar etmez (anemik model yasağı).
- **FR-006**: Orchestrator hiçbir hedef servisin veritabanına/aggregate'ine doğrudan erişmemelidir (BC izolasyonu).

**Haberleşme kanalı**

- **FR-007**: Orchestration adımları (durum-değiştiren komut + sonuç) **yalnızca message broker** üzerinden, asenkron komut/yanıt olarak yürütülmelidir; orchestrator bir adımın sonucunu beklerken bloke olmamalıdır.
- **FR-008**: Her yanıt, ait olduğu süreç örneğiyle güvenilir biçimde **ilişkilendirilebilmelidir** (korelasyon kimliği).
- **FR-009**: Senkron RPC yalnızca checkout-dışı, anlık-tutarlılık gerektiren mevcut sanksiyonlu akış (Basket→Stock rezervasyon, sepete atma anı) için kalır; checkout orchestration'ında senkron RPC kullanılmaz.

**Rezervasyon & stok kesinleştirme**

- **FR-010**: Stok müsaitlik kararı sepete atma anındaki rezervasyonda verilir; checkout **yeniden müsaitlik kontrolü yapmaz** — commit yalnız mevcut rezervasyonu (hold) kalıcı düşüşe çevirir.
- **FR-011**: Checkout girişinde, POST guard'ı geçersiz/dolmuş rezervasyonlu sepeti reddeder (saga başlamadan sepete döndürür). Saga içindeki commit adımı, yarış penceresinde düşmüş rezervasyonu telafi yoluna (FR-014) yönlendiren emniyet ağıdır.

**İki-fazlı ödeme**

- **FR-012**: Ödeme iki fazlı olmalıdır: önce **authorize** (blokaj), tüm kalemler kesinleştikten sonra **capture** (tahsil).
- **FR-013**: Bir pivot-öncesi adım başarısız olursa, tahsil edilmemiş blokaj **void** edilmelidir (gerçek para iadesi gerekmeden).
- **FR-014**: Payment bileşeni bir ödeme durum makinesi tutmalıdır: Authorized → Captured | Voided; geçersiz geçişler reddedilir.
- **FR-015**: Payment bileşeninin dış ödeme sağlayıcısına (PaymentGateway) giden gerçek para hop'u bu feature'da **stub'lanır** (yerel olarak Authorized/Captured/Voided döner); sınır net çizilir, ileride gerçek entegrasyona açık bırakılır.

**Telafi & pivot**

- **FR-016**: Pivot-öncesi bir adım kalıcı olarak başarısız olursa, önceki başarılı adımlar **ters sırada (LIFO)** geri sarılmalı, ödeme void edilmeli ve sipariş iptal edilmelidir.
- **FR-017**: Telafi her kaynak için tek geri-alma yapmalı ve **idempotent** olmalıdır (tekrar teslimde çift geri-alma olmaz).
- **FR-018**: **Pivot** = ödeme capture + sipariş onayı. Pivot sonrasında sipariş **asla iptal edilmez**; geç adımların (sepet temizliği) başarısızlığı siparişi Confirmed durumundan çıkarmaz.
- **FR-019**: Pivot sonrası geç adım başarısızlığı sınırlı kez tekrar denenir; tükenirse loglanıp süreç tamamlanır (log-and-complete).

**Dayanıklılık & tutarlılık**

- **FR-020**: Süreç durumu her adımda kalıcı olmalı; orchestrator restart'ında süreç kaldığı yerden devam etmeli, adım kaybı/çift-işleme olmamalıdır.
- **FR-021**: Durum değişikliği ile yayılan mesaj **atomik** olmalıdır (transactional outbox): sürecin ilerlediği ama mesajın yayılmadığı (veya tersi) durum oluşamaz.
- **FR-022**: Her komut idempotency anahtarı taşımalı; tüketiciler **aynı komutu tekrar işlememelidir** (idempotent consumer / inbox dedup).
- **FR-023**: Her adımın bir **zaman aşımı** olmalı; yanıt gelmezse adım hata sınıfına göre retry veya telafiye yönlendirilir. Sürecin tamamı için bir watchdog bulunmalıdır.
- **FR-024**: Geçici hatalar sınırlı kez, artan gecikmeyle (backoff) tekrar denenmeli; kalıcı zehirli mesajlar **dead-letter**'a taşınmalıdır.
- **FR-025**: Süreç, geçici (retry edilebilir) ile kalıcı (telafi/iptal gerektiren) hataları **ayırt etmelidir**.
- **FR-026**: Tamamlanmış bir sürece gelen geç mesajlar sessizce düşürülmeli (no-op), yan etki üretmemelidir.

**Alıcı deneyimi**

- **FR-027**: Alıcı checkout başlattığında hemen bir onay ("siparişin alındı, durumu takip edilebilir") almalı; süreç arka planda ilerlemelidir (senkron bekleme yok).
- **FR-028**: Sipariş nihai durumu (Confirmed/Cancelled) süreç bittiğinde alıcının sipariş listesinde doğru yansımalıdır; iptalde sebep görüntülenebilir olmalıdır.
- **FR-029**: Aynı sepet için mükerrer checkout başlatma engellenmeli (idempotent başlatma); ikinci istek yeni paralel süreç doğurmamalıdır.

### Key Entities *(include if feature involves data)*

- **Checkout Süreci (Saga)**: Bir siparişin checkout yaşam döngüsünün durumu. Nitelikler: süreç kimliği (sipariş kimliğiyle ilişkili), alıcı, kalemler, kesinleşen kalemler, mevcut faz (stok kesinleştirme / telafi / geç adım), deneme sayacı, ödeme referansı, telafi-başarısız bayrağı. Sürecin **tek doğruluk kaynağıdır** (saga log).
- **Ödeme (iki-fazlı)**: Bir checkout için ödeme durumu. Nitelikler: tutar, durum (Authorized/Captured/Voided), authorize referansı, idempotency anahtarı.
- **Sipariş**: Order BC'nin aggregate'i (bu feature onu değiştirmez, yalnız uzaktan tetikler). İlgili durumlar: Pending → Confirmed | Cancelled + iptal sebebi.
- **Rezervasyon**: Stock BC'nin geçici hold'u (sepete atma anında; TTL'li; commit ile kalıcı düşüşe çevrilir). Bu feature onu tüketir, oluşturmaz.
- **Orchestration Komutu / Yanıtı**: Adımı tetikleyen komut (idempotency anahtarı + korelasyon kimliği taşır) ve hedef servisin döndürdüğü sonuç (başarı/başarısızlık + hata sınıfı).
- **İşlenmiş-mesaj kaydı (inbox)**: Tüketici tarafında tekrar-işlemeyi önleyen dedup kaydı.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Başarılı checkout'ların %100'ünde nihai durum tutarlıdır — sipariş Confirmed, stok kalıcı düşmüş, ödeme Captured, sepet boş; hiçbir kombinasyon eksik kalmaz.
- **SC-002**: Pivot-öncesi başarısızlıkların %100'ünde gerçek para tahsil edilmez (yalnız void) ve stok net değişimi sıfırdır (geri sarma tam).
- **SC-003**: Pivot sonrası hiçbir senaryoda onaylanmış sipariş iptal olmaz (0 vaka).
- **SC-004**: Herhangi bir tek hedef servisin geçici kesintisi, checkout'un nihai doğru sonuca ulaşmasını engellemez; süreç servis dönünce tamamlanır.
- **SC-005**: Orchestrator süreç ortasında yeniden başlatıldığında, süreçlerin %100'ü tek ve doğru nihai duruma ulaşır (kayıp veya çift nihai durum yok).
- **SC-006**: Herhangi bir orchestration mesajının en-az-bir-kez tekrar teslimi çift yan etki üretmez (stok/ödeme tam bir kez uygulanır).
- **SC-007**: Alıcı checkout başlattıktan sonra onay ekranını 3 saniyeden kısa sürede görür (süreç senkron beklemez).

## Assumptions

- **Öğrenme/keşif kapsamı**: Bu feature ticari üretim için değil; gerçek para hareketi (PaymentGateway A2A hop'u) bilinçli olarak stub'lanır. Sınır net çizilir, ileride gerçek entegrasyon açık kalır.
- **Sıfırdan veri**: Geliştirme veritabanları sıfırdan kurulur; "full replace" cutover'ında akışta yarım kalmış (in-flight) eski saga örneği taşıma ihtiyacı yoktur.
- **Ödeme kaynağı**: Alıcının kayıtlı adres + kartı vardır (mevcut Customer/Wallet + 023 checkout deseni korunur); checkout kayıtlı seçimden ilerler.
- **Rezervasyon**: Sepet, checkout öncesi stok rezervasyonuna (012/014) sahiptir; commit bu rezervasyonu tüketir. TTL, süreç penceresini kapsayacak kadar uzundur; POST-öncesi dolarsa guard reddeder, saga-içi yarışta düşerse pivot-öncesi telafi geçerlidir.
- **Timeout/retry değerleri**: Makul varsayılanlar (adım başına birkaç saniye gecikme, birkaç deneme, dakikalar mertebesinde watchdog); kesin değerler plan/implementasyonda konfigüre edilebilir ayarlanır.
- **Broker**: Mevcut mesaj altyapısı (RabbitMQ) tüm orchestration komut/yanıtları için kullanılır; yayıncı exchange deklare eder, binding'i tüketici kurar (mevcut konvansiyon).
- **WebApp**: Checkout POST hedefi orchestration bileşenine taşınır; alıcı akışı (adres/kart seçimi → onay → "Siparişlerim") gözlemlenebilir biçimde korunur. Mevcut `Order/Create` guard'ı (boş sepet / dolmuş rezervasyon) korunur.
- **Anayasa uyumu**: İlke I checkout saga adımları için gRPC'yi örnekliyor; bu feature bilinçli olarak broker kanalını seçer (İlke I integration event kanalını da tanır). Bu sapma öğrenme amaçlıdır ve plan aşamasında Constitution Check'te gerekçelenir.