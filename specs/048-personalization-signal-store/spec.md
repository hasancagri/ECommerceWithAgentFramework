# Feature Specification: Personalization Signal Store (Faz 1)

**Feature Branch**: `048-personalization-signal-store`

**Created**: 2026-08-23

**Status**: Draft

**Input**: User description: "Personalization.Api — yeni .NET servisi (kendi Postgres/Marten DB'si), write-only davranış + satın-alma signal store. Faz 1 = sinyalleri DB'ye yazmak."

## Genel Bakış

Yeni bir bounded context (**Personalization**) kişiselleştirme için ham sinyalleri
biriktirir. Bu faz **yalnız yazma** (ingestion + kalıcılık): iki kaynaktan sinyal
alır ve kendi deposunda saklar. Öneri üretimi, segmentasyon, model eğitimi ve
kullanıcıya gösterim BU FAZDA YOKTUR (sonraki fazlar). Amaç: gelecekteki model/
kampanya çalışmasının besleneceği güvenilir sinyal deposunu kurmak.

## Clarifications

### Session 2026-08-24

- Q: Satın-alma sinyali hangi anda üretilsin (Order "tamamlandı" tam olarak ne)? → A:
  Ödeme onaylı + CheckoutSaga (028) başarıyla bittiğinde (ödeme onaylı, stok commit).
  Sinyal yalnız gerçek/ödenmiş satın-almayı yansıtır; oluşturulan ama ödenmemiş/iptal
  siparişler sayılmaz.
- Q: Yeni gezinme sinyalleri (CategoryViewed/BrandViewed/SearchPerformed) bu fazda
  WebApp'e eklensin mi? → A: Personalization.Api tüm sinyal tiplerini kabul eder, ancak
  WebApp bu fazda yalnız mevcut yakalama noktalarını (ProductViewed, ListShown,
  BasketItemAdded) HTTP hattına taşır. CategoryViewed/BrandViewed/SearchPerformed
  enstrümantasyonu sonraki faza bırakılır (endpoint şimdiden kabul eder).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Satın-alma sinyalinin kalıcı kaydı (Priority: P1)

Bir müşteri siparişini tamamladığında, ne satın aldığı (ürün, kategori, marka,
adet, tutar, tarih) kişiselleştirme deposuna **kayıpsız** yazılır. Bu, kampanya/
öneri için en güçlü sinyaldir ve para gerçeğidir; kaybolmamalıdır.

**Why this priority**: Satın-alma en yüksek niyet sinyali; RFM/segment ve öneri
motorunun temeli. Kayıpsız olması şart (davranıştan farklı). Tek başına bile
depoyu değerli kılar.

**Independent Test**: Bir sipariş tamamlanır; deponun o kullanıcı için satın-alma
kaydını (ürün/kategori/marka/adet/tutar/tarih) tam ve doğru içerdiği doğrulanır.
Personalization geçici kapalıyken tamamlanan sipariş, servis geri geldiğinde yine
kaydedilir (kayıpsız).

**Acceptance Scenarios**:

1. **Given** sepetinde ürünler olan giriş yapmış müşteri, **When** siparişi
   tamamlanır, **Then** her sipariş kalemi için ürün/kategori/marka/adet/birim
   tutar + sipariş tarihi + kullanıcı kimliği depoya yazılır.
2. **Given** aynı sipariş tamamlanma bildirimi tekrar işlenir (yeniden teslim),
   **When** kayıt denenir, **Then** mükerrer kayıt oluşmaz (idempotent).
3. **Given** sipariş tamamlandığı an Personalization deposu erişilemez, **When**
   servis tekrar erişilebilir olur, **Then** kayıt kaybolmadan işlenir.

---

### User Story 2 - Gezinme sinyalinin kaydı (Priority: P2)

Bir kullanıcı (giriş yapmış veya anonim) siteyi gezerken davranış sinyalleri
(ürün görüntüleme, liste gösterimi, kategori girişi, marka girişi, arama, sepete
ekleme) kişiselleştirme deposuna yazılır. Bu sinyaller **kayıp-toleranslıdır** ve
**sayfa akışını asla yavaşlatmaz/bozmaz**.

**Why this priority**: Gezinme, niyet ve ilgi sinyali sağlar (cold-start ve
içerik-benzeri öneri için). Yüksek hacimli; tekil kaybı önemsiz. Satın-almadan
sonra gelir çünkü tek başına kayıpsızlık gerektirmez.

**Independent Test**: Kullanıcı bir ürün sayfası açar; sinyalin depoya (makul kısa
gecikmeyle) yazıldığı doğrulanır. Personalization kapalı/yavaşken sayfa gecikmesiz
render olur ve hata vermez; sinyaller sessizce düşer.

**Acceptance Scenarios**:

1. **Given** bir kullanıcı ürün detayını açar, **When** sayfa yüklenir, **Then**
   ürün görüntüleme sinyali (ürün/kategori/marka/fiyat + kimlik) depoya yazılır.
2. **Given** kullanıcı bir listeyi görür veya sepete ürün ekler, **When** eylem
   gerçekleşir, **Then** ilgili sinyal tipiyle (ListShown / BasketItemAdded) kayıt
   yazılır. (CategoryViewed/BrandViewed/SearchPerformed arayüz enstrümantasyonu bu
   fazda YOK; endpoint yine de bu tipleri kabul eder.)
3. **Given** Personalization servisi kapalı veya yavaş, **When** kullanıcı gezinir,
   **Then** sayfa akışı bloklanmaz, hata görülmez; sinyaller kaybolabilir.
4. **Given** anlık sinyal yükü tampon kapasitesini aşar, **When** taşma olur,
   **Then** fazla sinyaller sessizce düşer; sayfa etkilenmez.

---

### User Story 3 - Alışveriş deneyimi Personalization'dan izole (Priority: P3)

Personalization deposu/servisi tamamen kapalı olsa bile, alışveriş deneyimi
(gezinme, sepet, sipariş) hiçbir şekilde bozulmaz. Kişiselleştirme sinyal toplama
tümüyle "en iyi çaba" bir yan etkidir; ana akışın önünde durmaz.

**Why this priority**: Güvenlik ağı. Yeni BC'nin mevcut sistemi kırmayacağının
garantisi. Bağımsız doğrulanabilir ve düşük riskli.

**Independent Test**: Personalization servisi durdurulur; tam bir alışveriş akışı
(gezin → sepete ekle → sipariş tamamla) hatasız tamamlanır. Satın-alma sinyali
servis geri gelince yakalanır; gezinme sinyalleri o pencere için kaybolabilir.

**Acceptance Scenarios**:

1. **Given** Personalization servisi kapalı, **When** kullanıcı sipariş tamamlar,
   **Then** sipariş başarıyla oluşur; satın-alma sinyali servis dönünce işlenir.
2. **Given** Personalization servisi kapalı, **When** kullanıcı gezinir, **Then**
   tüm sayfalar normal hız ve hatasız çalışır.

---

### Edge Cases

- **Anonim → giriş geçişi**: Anonim kimlikle toplanan sinyaller ile giriş sonrası
  kullanıcı kimliği ayrı tutulur; bu fazda kimlik birleştirme (identity stitching)
  YAPILMAZ, sadece mevcut kimlik alanları (kullanıcı / anonim / oturum) kaydedilir.
- **Kısmi sipariş verisi**: Sipariş olayında ürünün kategori/marka bilgisi yoksa,
  eksik alanlar boş bırakılır; kayıt yine yazılır (satın-alma kaybolmaz).
- **Tekrarlı olay teslimi**: Aynı sipariş olayı birden çok kez gelirse mükerrer
  kayıt engellenir (idempotent anahtar).
- **Yüksek gezinme hacmi**: Tampon dolduğunda fazla sinyal düşer; sistem sağlıklı
  kalır (geri-basınç ana akışa yansımaz).
- **Geçersiz/eksik sinyal gövdesi**: Bilinmeyen sinyal tipi veya zorunlu alanı
  eksik gövde reddedilir; diğer sinyaller etkilenmez.

## Requirements *(mandatory)*

### Functional Requirements

**Depo ve BC**

- **FR-001**: Sistem, kişiselleştirme sinyalleri için ayrı bir bounded context ve
  kendi kalıcı deposunu (diğer BC'lerle paylaşılmayan) sağlamalıdır.
- **FR-002**: Depo bu fazda **yalnız yazma amaçlıdır**; öneri/segment/model/
  kullanıcıya gösterim sağlamaz.

**Satın-alma sinyali (dayanıklı)**

- **FR-003**: Sistem, bir sipariş **ödeme onaylı olarak tamamlandığında** (CheckoutSaga
  başarı: ödeme onaylı + stok commit) satın-alma sinyalini kayıpsız şekilde depoya
  yazmalıdır. Oluşturulmuş ama ödenmemiş/iptal edilmiş siparişler sinyal üretmez.
- **FR-004**: Satın-alma sinyali kalem başına şunları içermelidir: ürün kimliği,
  kategori, marka, adet, birim tutar; sipariş düzeyinde: kullanıcı kimliği, sipariş
  tarihi/zamanı, sipariş kimliği.
- **FR-005**: Satın-alma sinyali işleme **idempotent** olmalı; aynı sipariş olayı
  yeniden işlense de mükerrer kayıt oluşmamalıdır.
- **FR-006**: Depo geçici erişilemezse satın-alma sinyali kaybolmamalı; erişim
  dönünce işlenmelidir (yeniden deneme / dayanıklı teslim).

**Gezinme sinyali (kayıp-toleranslı)**

- **FR-007**: Sistem (depo/endpoint) şu gezinme sinyal tiplerinin tümünü kabul edip
  yazabilmelidir: ProductViewed, ListShown, CategoryViewed, BrandViewed,
  SearchPerformed, BasketItemAdded.
- **FR-007a**: Bu fazda kullanıcı arayüzü yalnız mevcut yakalama noktalarından
  (ProductViewed, ListShown, BasketItemAdded) sinyal üretir. CategoryViewed /
  BrandViewed / SearchPerformed arayüz enstrümantasyonu sonraki faza bırakılır
  (endpoint bunları şimdiden kabul eder; veri sonra akmaya başlar).
- **FR-008**: Gezinme sinyali kaydı **kullanıcı arayüzü akışını bloklamamalı**;
  sinyal gönderimi ana istek/sayfa yanıtının önünde beklememelidir.
- **FR-009**: Personalization erişilemez/yavaş olduğunda gezinme sinyalleri sessizce
  düşebilir; kullanıcı hata görmez, sayfa gecikmez (kayıp-toleranslı).
- **FR-010**: Sistem, ani yük tamponu aştığında fazla gezinme sinyallerini düşürerek
  ana akışı korumalıdır (geri-basınç ana akışa sızmaz).
- **FR-011**: Gezinme sinyali şunları içermelidir: sinyal tipi, kullanıcı kimliği
  (varsa), anonim kimlik, oturum kimliği ve tipe uygun bağlam (ör. ürün kimliği,
  kategori, marka, fiyat, arama terimi, gösterilen ürün listesi).

**Gizlilik ve kimlik**

- **FR-012**: Hiçbir sinyal kişisel tanımlayıcı veri (ad, e-posta, adres, telefon,
  kart) içermemelidir; yalnız opak kullanıcı/anonim/oturum kimlikleri ve davranış/
  işlem alanları saklanır.
- **FR-013**: Sistem, geçersiz veya zorunlu alanı eksik sinyal gövdelerini
  reddetmeli ve diğer sinyallerin işlenmesini etkilememelidir.

**İzolasyon**

- **FR-014**: Personalization bileşeninin kısmen/tamamen kapalı olması, alışveriş
  ana akışını (gezinme, sepet, sipariş tamamlama) hiçbir biçimde bozmamalıdır.

### Key Entities *(include if feature involves data)*

- **BehaviorSignal (Gezinme sinyali)**: Kullanıcının bir gezinme etkileşimi.
  Alanlar: sinyal tipi, kullanıcı kimliği (ops.), anonim kimlik, oturum kimliği,
  zaman damgası ve tipe özgü bağlam (ürün/kategori/marka/fiyat/arama/liste). Kayıp-
  toleranslı; hacimli.
- **PurchaseSignal (Satın-alma sinyali)**: Tamamlanmış bir siparişin kişiselleştirme
  görünümü. Sipariş düzeyi: kullanıcı kimliği, sipariş kimliği (idempotent anahtar),
  sipariş tarihi. Kalem düzeyi (bir-çok): ürün kimliği, kategori, marka, adet, birim
  tutar. Kayıpsız; nadir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tamamlanan siparişlerin **%100'ü** (Personalization geçici kesinti
  senaryoları dahil, kurtarma sonrası) satın-alma sinyali olarak depoda yer alır;
  mükerrer kayıt **0**.
- **SC-002**: Gezinme sinyali toplama sayfa yanıt süresine **ölçülebilir gecikme
  eklemez** (sinyal gönderimi ana istek yolunda değildir; kullanıcı algısı
  değişmez).
- **SC-003**: Personalization servisi tamamen kapalıyken uçtan uca alışveriş akışı
  (gezin → sepete ekle → sipariş tamamla) **hatasız** tamamlanır; hata oranı **0**.
- **SC-004**: Normal yükte üretilen gezinme sinyallerinin depoya yazılma oranı
  yüksektir (kabul: taşma/kesinti dışında sinyaller kaydedilir); taşma yalnız
  tanımlı tampon aşımında gerçekleşir ve ana akışı etkilemez.
- **SC-005**: Depoda saklanan hiçbir kayıtta kişisel tanımlayıcı veri bulunmaz
  (denetimde PII alanı **0**).

## Assumptions

- **Order tarafında yeni bir "sipariş ödeme onaylı tamamlandı" bildirimi
  eklenecektir**; tetik noktası CheckoutSaga (028) başarı adımıdır (ödeme onaylı +
  stok commit). Şu an böyle yayınlanan bir olay yoktur (bu feature kapsamında Order
  tarafına ekleme yapılır). Sözleşme paylaşılan kontrat kütüphanesinde tanımlanır ve
  additive (geriye dönük uyumlu) tutulur.
- Gezinme sinyalleri WebApp'ten üretilir; bu fazda yalnız mevcut yakalama noktaları
  (ürün görüntüleme, liste gösterimi, sepete ekleme) HTTP hattına taşınır. Kategori/
  marka girişi ve arama sinyallerinin arayüz enstrümantasyonu sonraki fazdadır.
- Sinyal gönderimi WebApp tarafında ana istek akışından ayrı, tampon + arka plan
  ile yapılır (mevcut kayıp-toleranslı davranış-log deseni temel alınır).
- Mevcut Python personalization servisi (042) bu fazda değiştirilmez; ayrı kalır.
- Model eğitimi/öneri/segmentasyon ve Python'a veri aktarımı (dosya export) bu
  fazın **dışındadır**; sonraki fazlara bırakılmıştır.
- Demografik veri (yaş/cinsiyet) ve kayıt-onboarding tercih toplama bu fazın
  dışındadır.
- Kimlik birleştirme (anonim→kullanıcı geçmişini birleştirme) bu fazda yapılmaz.