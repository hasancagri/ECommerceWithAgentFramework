# Feature Specification: Hosted Checkout-Form Ödeme (hosted-CF)

**Feature Branch**: `077-hosted-cf-payment`

**Created**: 2026-09-13

**Status**: Draft

**Input**: User description: hosted-CF ödeme pivotu — "ödeme yap" → iyzico hosted ödeme linki → imzalı callback → mevcut checkout saga (AlreadyCaptured reuse). Kart saklama terk edildi; PAN yalnız iyzico hosted sayfada.

**Artefakt kademesi:** **Tam** — yeni aggregate (`PaymentIntent`), yeni integration event'ler (`PaymentSucceeded`/`PaymentFailed`), yeni dış kontrat (callback ucu + S2S link isteği), servisler-arası etki (Order↔Payment↔checkout saga). Full akış işletilir.

## Clarifications

### Session 2026-09-13

- Q: Aynı sepet/kullanıcı için Pending ödeme girişimi varken yeni "ödeme yap" gelince davranış ne? → A: Kullanıcı-kapsamlı re-use (Option A2) — aynı kullanıcı+sepet için Pending ödeme girişimi **hâlâ canlı** ise onun mevcut hosted bağlantısını döndür (yeni sipariş/girişim üretme); bağlantı bayatsa (ömrü dolmuş) eskiyi süresi-doldu işaretle + taze bağlantı üret. Ödeme girişimi ve bağlantısı daima isteği yapan kullanıcıya + siparişine bağlıdır; kim öderse ödesin sipariş sahibi değişmez.
- Q: PG→store callback ucu nasıl korunsun (yalnız HMAC / HMAC+token / yalnız token)? → A: Yalnız HMAC-SHA256 imza (ayrı CallbackSecret), geçersiz→401. Callback kullanıcı kimliği taşımaz; kullanıcı store'da referanstan türetilir (işlem referansı → ödeme girişimi → sipariş → kullanıcı). Sağlayıcı/store eşleşmesi kullanıcı kimliğiyle değil opak referansla kurulur: store işlem referansını (TxRef) üretir, sağlayıcı bunu ödeme sayfasına taşır ve sonuçta aynen geri döndürür — PSP'ye kullanıcı bilgisi sızmaz. Server webhook olduğu için oturum bağlamı yoktur; makine token'ı gereksiz.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Hosted ödeme linkiyle sipariş tamamlama (Priority: P1)

Müşteri kendi AI istemcisiyle sepetini hazırlar ve "ödeme yap" der. Sistem güvenli bir hosted ödeme sayfası bağlantısı üretir; müşteri kart bilgisini yalnız o dış sayfada girer. Ödeme başarılı olunca sipariş kendiliğinden tamamlanır (stok düşer, sipariş onaylanır, sepet temizlenir). Kart bilgisi mağazada hiçbir zaman görünmez/saklanmaz.

**Why this priority**: Mağazanın satış yapabilmesinin tek yolu; ödeme olmadan hiçbir sipariş tamamlanamaz. Bu akış olmadan ürün satılamaz.

**Independent Test**: Sepette ürün olan giriş yapmış müşteri "ödeme yap" der → hosted URL döner → o sayfada ödeme yapılınca sipariş Confirmed olur, stok düşer, sepet boşalır. Tek başına uçtan uca değer üretir.

**Acceptance Scenarios**:

1. **Given** sepette ürün olan giriş yapmış müşteri, **When** "ödeme yap" der, **Then** sistem sipariş taslağını (Pending) oluşturur ve müşteriye tıklanabilir bir hosted ödeme bağlantısı döner.
2. **Given** geçerli hosted ödeme bağlantısı, **When** müşteri o sayfada ödemeyi başarıyla tamamlar, **Then** ödeme sağlayıcısından gelen başarılı sinyali üzerine sipariş onaylanır, stok düşer, sepet temizlenir.
3. **Given** ödeme başarıyla tamamlanmış bir işlem, **When** aynı başarı sinyali (aynı işlem referansıyla) ikinci kez gelir, **Then** sistem tek sipariş/tek stok düşümü üretir (tekrar işlenmez).
4. **Given** ödeme tamamlanmış müşteri, **When** siparişlerini görüntüler, **Then** sipariş "onaylandı" durumunda ve doğru tutar/ürünlerle listelenir.

---

### User Story 2 - Başarısız veya terk edilmiş ödemede sipariş iptali (Priority: P1)

Müşteri hosted sayfaya gider ama ödemez (sayfayı kapatır / süre dolar) ya da kart reddedilir. Sistem bu durumu güvenle tespit eder ve taslak siparişi iptal eder; stok hiçbir zaman düşmediği ve para hareket etmediği için kayıp oluşmaz.

**Why this priority**: Ödeme akışının doğru olması, mutlu yolun tamamlanması kadar kritik; terk/başarısızlık yanlış yönetilirse asılı kalan siparişler ve tutarsız durum oluşur.

**Independent Test**: Hosted link üretilir, müşteri ödemez → zaman aşımı sonrası sipariş iptal olur, stok değişmez. Kart reddi sinyali gelirse aynı sonuç.

**Acceptance Scenarios**:

1. **Given** ödeme bağlantısı üretilmiş taslak sipariş, **When** yapılandırılmış süre boyunca (varsayılan 5 dakika) hiçbir ödeme sonucu gelmez, **Then** ödeme girişimi "süresi doldu" olarak işaretlenir ve sipariş iptal edilir; stok değişmez.
2. **Given** hosted sayfada başarısız (reddedilmiş) ödeme, **When** başarısızlık sinyali gelir, **Then** sipariş iptal edilir ve stok değişmez.
3. **Given** süresi dolmuş bir ödeme girişimi, **When** geç bir başarı sinyali gelir, **Then** sonuç idempotent kurallarla değerlendirilir ve çift sipariş/çift işlem oluşmaz.

---

### User Story 3 - Sahte ödeme bildirimine karşı koruma (Priority: P2)

Ödeme sonucu bildirimi (callback) internete açık bir uçtur. Sistem yalnızca gerçek ödeme sağlayıcısından gelen, imzası doğrulanmış bildirimleri kabul eder; sahte "ödendi" bildirimleri reddedilir.

**Why this priority**: Güvenlik. Doğrulama olmazsa saldırgan ödeme yapmadan sipariş tamamlatabilir (bedava mal). MVP satışın hemen ardından gelen zorunlu güvence.

**Independent Test**: Geçerli imzalı bildirim işlenir; geçersiz/eksik imzalı bildirim reddedilir (işlem yapılmaz), sipariş durumu değişmez.

**Acceptance Scenarios**:

1. **Given** geçerli imza taşıyan bir ödeme bildirimi, **When** callback ucuna ulaşır, **Then** bildirim işlenir.
2. **Given** geçersiz veya eksik imzalı bir ödeme bildirimi, **When** callback ucuna ulaşır, **Then** bildirim reddedilir, hiçbir sipariş/ödeme durumu değişmez.

---

### Edge Cases

- **Son nüsha yarışı (aşırı-satış):** Aynı son ürün için iki müşteri de "ödeme yap" der ve ikisi de öder. Stok ödeme başarısından sonra düştüğü için biri stok-yetersiz kalır. Bu durumda o sipariş tamamlanamaz; olay loglanır ve manuel müdahale/iade konusu olur (otomatik iade v1 kapsamı dışı). Aşırı-satış nadir kabul edilir (bkz Assumptions).
- **Ödeme başarılı ama sonraki adım (onay/sepet temizleme) başarısız:** Para alınmıştır; sipariş onaylı kalır, olay loglanır (ileri-tamamla, geri-alma yok).
- **Link üretilemiyor** (ödeme sağlayıcısına ulaşılamıyor): "ödeme yap" hata döner; taslak sipariş oluşmadıysa hiç, oluştuysa iptal edilir. Para/stok hareketi yok.
- **Çift bildirim** (sağlayıcı retry eder): İşlem referansı tekilliği + durum kontrolü ile ikinci bildirim etkisizdir.
- **Bağlantı çoğaltma (aynı link N kopya / N sekme):** Aynı hosted bağlantı tek ödeme oturumuna (tek işlem referansı → tek sipariş) karşılık gelir. En fazla **bir** başarılı ödeme + **bir** tamamlanmış sipariş oluşur: sağlayıcının ödeme-oturumu tek-kullanımlığı çift-çekimi (ikinci kart çekimini) engeller *(sağlayıcı tarafında teyit edilir — kapsam dışı)*; store idempotency'si (işlem referansı tekilliği + durum kontrolü) çift-sipariş tamamlamayı garanti keser.
- **Tekrarlı "ödeme yap" canlı bağlantıyla:** Kullanıcı bağlantı hâlâ geçerliyken yeniden "ödeme yap" derse aynı bağlantı döner (yeni sipariş/çekim oluşmaz).
- **Boş sepet:** Sepet boşsa "ödeme yap" sipariş/ödeme girişimi oluşturmaz; **hata/exception fırlatmaz**, müşteriye dostça bir yönlendirme mesajı döner (ör. "Lütfen sepete ürün ekleyiniz").
- **Geç bildirim vs zaman aşımı yarışı:** Hangisi önce kesinleşirse o kazanır; diğeri durum kontrolüne takılır — çift sonuç yok.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, giriş yapmış müşterinin "ödeme yap" isteğinde sepet içeriğinden bir sipariş taslağını **Pending** durumunda oluşturmalı ve bu siparişe bağlı bir ödeme girişimi başlatmalıdır.
- **FR-001a**: Sistem "ödeme yap" isteğinde, aynı kullanıcı+sepet için **hâlâ canlı** (bağlantısı geçerli, beklemede) bir ödeme girişimi varsa yeni sipariş/girişim üretmemeli, mevcut girişimin hosted bağlantısını döndürmelidir (kullanıcı-kapsamlı idempotent re-use). Mevcut girişimin bağlantısı bayatsa (ömrü dolmuş) sistem eskisini süresi-doldu işaretlemeli ve taze bağlantılı yeni girişim üretmelidir. "Canlı/bayat" ölçütü bağlantının ömrüdür.
- **FR-001b**: Ödeme girişimi ve hosted bağlantısı daima isteği yapan kullanıcıya ve onun siparişine bağlı olmalıdır; bağlantıyı fiilen kim öderse ödesin (bağlantı paylaşılsa dahi) tamamlanan sipariş isteği yapan kullanıcıya aittir ve onun adresine gider — ödeme, siparişin sahibini değiştirmez.
- **FR-002**: Sistem her ödeme girişimi için tekil bir işlem referansı üretmeli (mağaza tarafında üretilir) ve bunu ödeme sağlayıcısına ilettiği isteğe koymalıdır.
- **FR-003**: Sistem, ödeme sağlayıcısından bir hosted ödeme sayfası bağlantısı almalı ve bu bağlantıyı isteği yapan müşteriye (agent'a) tool sonucu olarak döndürmelidir.
- **FR-004**: Kart bilgisi (PAN vb.) mağazanın hiçbir bileşeninde görünmemeli, taşınmamalı veya saklanmamalıdır; kart yalnız ödeme sağlayıcısının hosted sayfasında girilir.
- **FR-005**: Sistem, ödeme sağlayıcısından gelen ödeme-sonucu bildirimini (callback) kabul eden bir uç sağlamalı ve bu bildirimin gerçekliğini paylaşılan bir gizli anahtarla üretilmiş imza doğrulamasıyla teyit etmelidir; imzası geçersiz/eksik bildirim reddedilir (hiçbir durum değişmez).
- **FR-006**: İmza doğrulaması için kullanılan gizli anahtar, giden istek kimlik anahtarından (MerchantKey) **ayrı** bir callback gizli anahtarı olmalıdır.
- **FR-007**: Başarılı ödeme bildiriminde sistem ödeme girişimini "başarılı" olarak işaretlemeli ve bunun sonucunda siparişin tamamlanma sürecini (stok düşümü, sipariş onayı, sepet temizliği) tetiklemelidir.
- **FR-008**: Bildirim işleme idempotent olmalıdır: aynı işlem referansıyla gelen tekrarlı bildirimler tek sonuç üretmeli (çift sipariş tamamlama / çift stok düşümü olmamalı).
- **FR-009**: Bildirim işleme dayanıklı olmalıdır: ödeme sonucunun kaydı ile buna bağlı tamamlama tetiği ya birlikte kalıcı olur ya da hiç olmaz (kısmi durum bırakmaz); bileşen çökse bile onaylanmış sonuç kaybolmaz.
- **FR-010**: Başarısız (reddedilmiş) ödeme bildiriminde sistem ödeme girişimini "başarısız" işaretlemeli ve ilgili taslak siparişi iptal etmelidir; stok değişmez.
- **FR-011**: Sistem, ödeme girişimi başladığı anda yapılandırılabilir bir süre (varsayılan 5 dakika) için terk-zaman-aşımı kurmalıdır; süre dolduğunda girişim hâlâ "beklemede" ise "süresi doldu" işaretlenir ve taslak sipariş iptal edilir (stok değişmez). Girişim bu arada sonuçlandıysa zaman aşımı etkisizdir.
- **FR-012**: Ödeme başarısından sonra tetiklenen sipariş-tamamlama süreci, mevcut checkout sürecinin "ödeme dışarıda alınmış" (AlreadyCaptured) yolunu kullanmalıdır: stok düşümü → sipariş onayı → sepet temizliği, mevcut telafi ve zaman-aşımı gözcüsü davranışıyla birlikte.
- **FR-013**: Stok, "ödeme yap" anında **rezerve edilmez/kilitlenmez**; stok düşümü ödeme başarısından sonra (tamamlama sürecinde) gerçekleşir.
- **FR-014**: Ödeme başarısından sonra stok düşümü başarısız olursa (aşırı-satış), sistem olayı loglamalı; sipariş tamamlanamaz ve manuel müdahale/iade konusu olur (otomatik iade v1 kapsamı dışı — bkz Assumptions).
- **FR-015**: Sistem, başarılı ve başarısız/terk edilmiş ödeme sonuçlarını, sipariş tamamlama/iptal sürecini tetikleyen ayrı bildirimler olarak yayımlamalıdır (başarı → tamamlama; başarısızlık/terk → iptal).
- **FR-016**: Müşteri, tamamlanmış siparişini "onaylandı" durumu, doğru tutar ve ürünlerle görüntüleyebilmelidir.
- **FR-018**: Sepeti boş bir müşteri "ödeme yap" dediğinde sistem **hata/exception fırlatmamalı**; sipariş/ödeme girişimi oluşturmadan, sepete ürün eklemeye yönlendiren dostça bir bilgi mesajı döndürmelidir (beklenen durum, Result ile taşınır).

### Sökülecek Davranış (bu feature kapsamında kaldırılır)

- **FR-017**: Eski mock tek-faz tahsilat yolu (mağaza içi anlık "ödeme çek" adımı) ve checkout sürecinin buna bağlı "Charge" modu — komutları, ödeme çekme yanıtı, "tahsilat" fazı ve ilgili handler'ları — tümüyle kaldırılmalıdır; bu feature sonrasında ödeme yalnızca hosted-CF yoluyla (AlreadyCaptured) gerçekleşir.

### Key Entities *(include if feature involves data)*

- **PaymentIntent (Ödeme Girişimi)**: Bir siparişin ödeme çabasını temsil eder. Öznitelikler: sipariş referansı, kullanıcı, tutar, mağaza-üretimli tekil işlem referansı, sağlayıcı ödeme referansı, hosted ödeme bağlantısı, durum (Beklemede / Başarılı / Başarısız / Süresi-doldu). Yaşam döngüsü: Beklemede doğar; başarılı bildirimle Başarılı (idempotent, terminal); reddedilmişte Başarısız; süre dolunca Süresi-doldu. Başarılı olduktan sonra durumu geri alınamaz. İşlem referansı tekildir.
- **Ödeme sonucu bildirimi (event)**: Başarı → sipariş tamamlamayı tetikler; başarısızlık/terk → sipariş iptalini tetikler. Sipariş referansı, ödeme girişimi kimliği, işlem referansı ve (başarısızlıkta) sebep taşır.
- **Sipariş (mevcut, dokunulan durum)**: Bu feature onu **Pending** doğurur; ödeme başarısıyla mevcut tamamlama süreci onu onaylıya götürür; başarısızlık/terkte iptal olur.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Müşteri "ödeme yap" dedikten sonra kullanılabilir bir hosted ödeme bağlantısını birkaç saniye içinde (uçta ölçülen normal koşullarda ≤ 5 sn) alır.
- **SC-002**: Ödeme başarısı bildiriminden sonra sipariş, insan müdahalesi olmadan otomatik olarak onaylanır (stok düşer, sepet temizlenir).
- **SC-003**: Ödemeyi terk eden hiçbir müşteri için stok düşmez ve tamamlanmamış (asılı) sipariş kalmaz; terk edilen her girişim yapılandırılmış süre içinde iptale döner.
- **SC-004**: Aynı ödeme için tekrarlı bildirim(ler), müşterinin siparişlerinde yalnızca **tek** tamamlanmış sipariş üretir (çift tamamlama %0).
- **SC-005**: İmzası doğrulanmayan hiçbir ödeme bildirimi sipariş/ödeme durumunu değiştiremez (yetkisiz tamamlama %0).
- **SC-006**: Mağazanın hiçbir kaydında/logunda kart PAN'ı bulunmaz (%0).

## Assumptions

- **Ödeme sağlayıcısı (PG) tarafı bu spec'in kapsamı dışıdır.** Hosted-payment başlatma, ödeme-durum sorgusu, callback yayımı ve iyzico hosted checkout-form entegrasyonu ayrı bir repo/PR'da (DropShop PaymentGateway) yapılır. Bu spec mağaza (store) tarafını kapsar ve PG'nin bu yetenekleri sağladığını varsayar.
- **Aşırı-satış toleransı:** First-party kitapçı, stok bol; son-nüsha yarışı nadir. v1 stok kilidi kurmaz (mevcut "stok tutma yok" yönüyle tutarlı). Aşırı-satış olursa manuel/iade. Kilit gerekirse ayrı bir sonraki iş (erken-rezerv Seçenek B) — kapsam dışı.
- **İade otomasyonu kapsam dışıdır** (backlog): ödeme sonrası nadir stok-yetersizliğinde iade elle yapılır.
- **Callback yedek yoklama (poll) kapsam dışıdır** (backlog): v1 dayanıklılığı imzalı callback + terk-zaman-aşımı ile sağlar; PG↔store callback tamamen kesilirse manuel telafi.
- **Terk-zaman-aşımı varsayılanı 5 dakika**, yapılandırılabilir; hosted oturum ömrü + kullanıcının kart girme/3D-doğrulama payını kapsar.
- **İşlem referansını mağaza üretir** (tekil kimlik); sağlayıcı referansı ayrıca saklanır (iz/destek).
- **Ödeme başarısı = pivot:** para alındıktan sonra süreç yalnız ileri tamamlanır; mağaza içinden geri-alma (void/refund) yoktur.
- **Sipariş tamamlama için mevcut checkout süreci (AlreadyCaptured yolu) yeniden kullanılır**; yeniden sıralanmaz.
- **Kimlik/oturum mevcut sistemle sağlanır**; "ödeme yap" yüzeyi giriş yapmış kullanıcı gerektirir.