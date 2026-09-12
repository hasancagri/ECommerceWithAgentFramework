# Feature Specification: PG Aracılı Kart Saklama (A yolu — ince Wallet)

**Feature Branch**: `075-iyzico-card-storage`

**Created**: 2026-09-12

**Status**: Draft

**Input**: User description: "Kart saklama; müşteri tüm yüzeyi Claude Desktop üzerinden mcp-gateway ile yürütür. Mağaza (E-Commerce) PaymentGateway (PG) ile konuşur; PG içeride iyzico ile haberleşir. Kart eklemek istendiğinde PG iyzico hosted form linkini alıp mağazaya verir, kullanıcı tarayıcıda linke tıklayıp kartı iyzico ekranında girer. PAN ne Claude Desktop'a ne mağazaya uğrar. Wallet yalnız UserId↔PG-kullanıcı-handle çapasını + varsayılan kart handle'ını saklar; kart listesi PG'den canlı çekilir. Güncelleme yok (sil+ekle)."

## Clarifications

### Session 2026-09-12

- Q: Kullanıcı sil/varsayılan için chat'te kartı nasıl işaret eder (token dönmeden)? → A: PG'nin kart handle'ı (opak) döner; PAN/CVV asla dönmez. Handle tek başına (merchant anahtarı olmadan) çekim yapamaz, sahibe göstermek güvenli.
- Q: Kart ekleme ne zaman olur — standalone mı, yalnız checkout'ta mı? → A: Standalone "kart ekle" her zaman; PG iyzico hosted ekranını nominal doğrulama işlemiyle (çekim değil) açar, kullanıcı alışveriş yapmadan kart kaydeder.
- Q: İlk kart eklendiğinde otomatik varsayılan olsun mu? → A: Evet; kullanıcının ilk kartı otomatik varsayılan olur.
- Q: iyzico'yu kim çağırır? → A: **PG çağırır** (harici DropShop repo). Mağaza yalnız PG ile REST konuşur; iyzico SDK bu repoda YOK. Değiştirilebilir seam PG'nin içinde (bugün iyzico, yarın kendi geliştirme/banka) — mağaza-PG sözleşmesi değişmez.
- Q: 075 kapsamı hangi repo? → A: **Bu repo (mağaza tarafı)**; PG'nin kart uçları (add-session/list/delete/charge) tüketilir. PG-içi iyzico işi ayrı fasıl/ayrı repo.
- Q: Çekim yolu — bugünkü iki yol (Order.Api `PlaceOrderForAgent` direkt çekim vs checkout saga→mock Payment BC) hangisi canonical? → A: **saga→PG.** Çekim sahibi Payment BC olur (saga `ChargePaymentCommand` → Payment BC → PG NON-3D; mock kalkar). `PlaceOrderForAgent` direkt `gateway.ChargeAsync` **kaldırılır** → `confirmed` onayı sonrası yalnız `StartCheckout` yayınlar. `PaymentGatewayClient` + payment-context client Order.Api'den **Payment.Api'ye taşınır**.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kartı iyzico'nun ekranında (PG linkiyle) kaydetme (Priority: P1)

Giriş yapmış müşteri, Claude Desktop'ta "kart eklemek istiyorum" der. Mağaza (E-Commerce) PG'den bir
kart-ekleme oturumu ister; PG iyzico ile haberleşip barındırılan güvenli kart ekranının **linkini**
alır ve mağazaya döner. Müşteri linki tarayıcıda açar, kart bilgilerini **yalnız iyzico'nun ekranında**
girer. iyzico kartı saklar; PG mağazaya bir **kullanıcı-handle** (kullanıcının kart kümesini temsil
eder) döner; mağaza bunu kullanıcının cüzdanında kalıcılaştırır. Müşteri sohbete döndüğünde kart
kayıtlı kartlar arasında görünür.

**Why this priority**: Kart saklamanın çekirdek değeri ve uyum kısıtının (PAN mağazaya/sohbete uğramaz)
sınandığı yol. Tek başına anlamlı MVP: müşteri kart kaydedebiliyor.

**Independent Test**: Sandbox test kartıyla ekleme linki alınır, kart iyzico ekranında girilir, dönüşte
kayıtlı kartlar listesinde marka+son4 ile görünür.

**Acceptance Scenarios**:

1. **Given** giriş yapmış, hiç kartı olmayan kullanıcı, **When** kart ekleme başlatır ve iyzico
   ekranında geçerli test kartını girer, **Then** kart kaydedilir ve kullanıcı için PG kullanıcı-handle
   ilk kez kalıcılaşır.
2. **Given** zaten bir kartı olan kullanıcı, **When** ikinci kartı ekler, **Then** ikinci kart **aynı**
   kullanıcı-handle'a bağlanır (yeni handle üretilmez).
3. **Given** kullanıcı ekleme linkini aldı ama iyzico ekranını **iptal etti**, **When** sohbete döner,
   **Then** kayıtlı kart eklenmemiştir ve sistem net "eklenmedi" durumu bildirir.
4. **Given** kullanıcı akışı, **When** herhangi bir adım gerçekleşir, **Then** ham kart numarası/CVV
   hiçbir noktada MCP tool argümanında, sohbet metninde veya mağaza kaydında yer almaz.

---

### User Story 2 - Kayıtlı kartları listeleme (Priority: P1)

Giriş yapmış müşteri, Claude Desktop'ta kayıtlı kartlarını sorar. Mağaza, kullanıcının PG kullanıcı-
handle'ı ile PG'den kart listesini **canlı** çeker (PG içeride iyzico'dan alır) ve yalnız gösterilebilir
alanlarla (marka, son 4 hane, son-kullanma, etiket) + kartı işaret etmek için opak kart-handle döner.
Mağaza yerelde kart deposu tutmaz; tek gerçek-kaynak PG/iyzico.

**Why this priority**: Eklemenin karşılığı; kullanıcı ne kaydettiğini görmeden akış tamamlanmaz.

**Independent Test**: Bir kart kayıtlıyken listeleme çağrılır; marka+son4+SKT + opak handle döner, ham
PAN/CVV dönmez.

**Acceptance Scenarios**:

1. **Given** iki kartı kayıtlı kullanıcı, **When** kartları listeler, **Then** iki kart da marka + son
   4 hane + son-kullanma + opak kart-handle ile döner; ham PAN/CVV dönmez.
2. **Given** hiç kartı olmayan kullanıcı, **When** listeler, **Then** boş liste + "kayıtlı kart yok"
   durumu döner (hata değil).

---

### User Story 3 - Kayıtlı kartı silme (Priority: P2)

Giriş yapmış müşteri, belirli bir kayıtlı kartı siler. Mağaza, kart verisi taşımadan (PG kullanıcı-
handle + kart-handle ile) PG'ye silme isteği yapar (PG içeride iyzico'da siler). Tarayıcıya yönlendirme
yoktur.

**Why this priority**: Yönetimi tamamlar; MVP için ekleme + listeleme yeterli, bu yüzden P2.

**Independent Test**: Kayıtlı bir kart silinir; sonraki listede görünmez.

**Acceptance Scenarios**:

1. **Given** iki kartı kayıtlı kullanıcı, **When** birini siler, **Then** o kart PG üzerinden kaldırılır
   ve sonraki listede yer almaz; diğer kart durur.
2. **Given** kullanıcı, **When** kendisine ait olmayan/bulunmayan bir kartı silmeye çalışır, **Then**
   işlem reddedilir ve başka kullanıcının kartı etkilenmez.

---

### User Story 4 - Varsayılan kart seçimi (Priority: P2)

Giriş yapmış müşteri, kayıtlı kartlarından birini varsayılan yapar. Mağaza bu seçimi cüzdanında (yalnız
opak kart-handle olarak; PAN yok) tutar. Ödeme anında, kullanıcı aksini belirtmezse varsayılan kart
kullanılır (tek-tık).

**Why this priority**: Tek-tık ödeme deneyimini açar; ekleme+listeleme MVP'sinin hemen üstüne biner.

**Independent Test**: İki karttan biri varsayılan yapılır; ödeme bağlamı çekildiğinde varsayılan kartın
handle'ı döner.

**Acceptance Scenarios**:

1. **Given** iki kartı kayıtlı kullanıcı, **When** birini varsayılan yapar, **Then** cüzdanda o kartın
   handle'ı varsayılan olarak tutulur; en fazla bir varsayılan olur.
2. **Given** varsayılan kartı olan kullanıcı, **When** o kartı siler, **Then** varsayılan seçim temizlenir
   (bayat handle varsayılan kalmaz).

---

### User Story 5 - Sipariş ödeme bağlamı + NON-3D çekim (agent onaylı) (Priority: P3)

Sipariş anında: (1) Sipariş servisi yapısal servis-servis kanaldan ödeme bağlamını çeker — bağlam,
seçilen (veya varsayılan) kartın PG kullanıcı-handle + kart-handle'ını içerir. (2) Çekim başlatılmadan
önce müşteriden Claude Desktop'ta **açık onay** alınır (tutar + kartın son 4 hanesi gösterilir);
onaylanınca mağaza PG'ye çekim isteği yapar, PG içeride iyzico'da **NON-3D** (3DS'siz) tek çekim yapar.

**Why this priority**: Saklı kartın nihai amacı; ama ekleme/listeleme/varsayılan çalışmadan anlamsız.

**Independent Test**: Sandbox kartıyla, onay verilmiş bir siparişte NON-3D çekim başarılı döner; onay
verilmezse çekim hiç başlamaz.

**Acceptance Scenarios**:

1. **Given** varsayılan kartı olan kullanıcı, **When** ödeme bağlamı çekilir, **Then** bağlam varsayılan
   kartın PG kullanıcı-handle + kart-handle'ını içerir, ham PAN içermez.
2. **Given** çekim öncesi onay adımı, **When** müşteri tutarı + kartı onaylar, **Then** PG üzerinden
   NON-3D çekim başlatılır; **When** onaylamaz/iptal eder, **Then** çekim hiç başlamaz ve sipariş
   ilerlemez.
3. **Given** NON-3D çekim, **When** PG başarı/başarısızlık döner, **Then** checkout sağası mevcut
   Charge-pivot davranışına göre ilerler/telafi eder (3DS ekranı devreye girmez).

---

### Edge Cases

- **PG/iyzico erişilemez (listeleme):** Kart listesi canlı çekildiği için kesintide liste alınamaz →
  kullanıcıya "kartlar şu an getirilemiyor, tekrar dene" bildirilir (bayat yerel kopya yok).
- **Ekleme linki süre aşımı:** Hosted ekran linki süre sonunda geçersiz → yeni ekleme başlatılır.
- **Callback geldi ama kullanıcı sohbete dönmedi:** Kayıt yine de kalıcılaşır; kullanıcı sonra
  listelediğinde kartı görür (ekleme fire-and-forget; onay listede görünür).
- **Yinelenen kart:** Aynı kartı ikinci ekleme → PG/iyzico davranışına bırakılır; mağaza ayrı
  tekilleştirme yapmaz.
- **Silinen son kart:** Kullanıcının hiç kartı kalmaması geçerlidir; PG kullanıcı-handle çapası
  Wallet'ta kalabilir (sonraki eklemede yeniden kullanılır).
- **Kart son-kullanma geçmiş:** Listede görünür ama ödeme anında PG/iyzico reddeder; feature ayrı
  "süresi geçti" filtresi zorunlu kılmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, giriş yapmış kullanıcının kart ekleme isteğinde (alışverişe bağlı OLMADAN,
  standalone) PG'den barındırılan kart ekranı için tek-kullanımlık bir **link/oturum** alıp
  döndürMELİ. Ekran nominal doğrulama işlemiyle açılabilir; bu bir **çekim değildir**.
- **FR-001a**: Kullanıcının **ilk** kartı eklendiğinde otomatik olarak varsayılan kart olMALI.
- **FR-002**: Ham kart verisi (kart numarası, CVV, son-kullanma) yalnız iyzico'nun barındırdığı ekranda
  toplanMALI; hiçbir kart alanı MCP tool argümanında, sohbet metninde veya mağaza kaydında yer
  almaMALI (uyum kısıtı — pazarlıksız).
- **FR-003**: Kart başarıyla saklandığında sistem, PG'nin döndürdüğü **kullanıcı-handle**'ı o kullanıcı
  için kalıcılaştırMALI; kullanıcının sonraki kartları **aynı** handle'a bağlanMALI.
- **FR-004**: Sistem yerelde ham PAN, CVV veya kart listesi kopyasını kalıcı tutMAMALI; kart listesi
  her istekte PG'den **canlı** çekilMELİ (tek gerçek-kaynak PG/iyzico). Yerelde tutulan tek kart-ilişkili
  veri = PG kullanıcı-handle + varsayılan kart-handle.
- **FR-005**: Sistem kayıtlı kartları listelerken gösterilebilir alanları (marka, son 4 hane,
  son-kullanma, etiket) + kartı işaret etmek için opak **kart-handle**'ı döndürMELİ; ham PAN/CVV asla
  döndürMEMELİ. (Handle tek başına, merchant API anahtarı olmadan çekim yapamaz — sahibe göstermek
  güvenlidir.)
- **FR-006**: Sistem, kart silmeyi kart verisi taşımadan (kullanıcı-handle + kart-handle ile) PG
  üzerinden gerçekleştirMELİ; silme tarayıcı yönlendirmesi gerektirMEMELİ.
- **FR-007**: Sistem ayrı bir "kart güncelleme" işlemi sunMAMALI; değişiklik sil + yeniden ekle ile
  yapılMALI.
- **FR-008**: Kart yönetimi (ekle/listele/sil/varsayılan) yalnız giriş yapmış kullanıcının **kendi**
  kartlarına etki etMELİ; başka kullanıcının kartına erişim/etki engellenMELİ.
- **FR-009**: Sipariş servisi ödeme bağlamını yapısal servis-servis kanaldan çektiğinde, bağlam seçilen
  kartın PG kullanıcı-handle + kart-handle'ını içerMELİ; ham PAN içerMEMELİ.
- **FR-010**: Kart ekleme iptal edilir/başarısız olursa sistem net "eklenmedi" durumu döndürMELİ ve
  kalıcı kayıt yapMAMALI.
- **FR-011**: Uçtan uca akış PG'nin sandbox iyzico yapılandırmasıyla test kartları üzerinden
  çalışabilMELİ; mağaza-PG sözleşmesi ortamdan bağımsız olMALI.
- **FR-012**: Sistem, kullanıcı başına en fazla **bir varsayılan kartı** cüzdanda yalnız opak kart-handle
  olarak tutMALI (PAN yok); yeni varsayılan seçimi öncekini temizleMELİ. Varsayılan kart silindiğinde
  varsayılan seçim temizlenMELİ.
- **FR-013**: Sistem, sipariş çekimini **checkout sağası → Payment BC → PG NON-3D** yolundan yapMALI
  (kullanıcı-handle + kart-handle ile); Payment BC çekimin sahibidir (mock kalkar), saga Charge-pivot
  davranışı korunur (ayrı orchestration servisi açılmaz). Agent `place_order` **direkt çekmez** —
  onay sonrası `StartCheckout` yayınlar. PG içeride iyzico NON-3D çekimini yapar.
- **FR-014**: Sistem, çekim başlatılmadan önce müşteriden Claude Desktop'ta **açık onay** almaLI (en az
  tutar + kartın son 4 hanesi gösterilir); onay yoksa/iptal edilirse çekim başlaMAMALI. Bu onay,
  3DS'nin yerini tutan tek doğrulama adımıdır (NON-3D'de banka doğrulama ekranı yoktur).
- **FR-015**: NON-3D çekimde fraud sorumluluğunun tüccarda/PG'de olduğu **bilinçli sandbox/demo
  kararıdır**; bu feature prod-seviyesi fraud duruşu (3DS zorunluluğu) getirMEZ.
- **FR-016**: Mağaza kod tabanında iyzico'ya doğrudan bağımlılık (SDK/HTTP) bulunMAMALI; iyzico erişimi
  yalnız PG'nin ardındadır (değiştirilebilir seam PG'de). Mağaza yalnız PG'nin kart sözleşmesini bilir.

### Key Entities *(include if feature involves data)*

- **Wallet (ince):** Kullanıcı başına tek kayıt; `UserId` + **PG kullanıcı-handle** + **varsayılan
  kart-handle**. Ham PAN/CVV veya kart listesi deposu YOK. Adres defteri aynı BC'de, bu feature'la
  değişmez.
- **Kayıtlı kart (PG/iyzico'da, mağazada değil):** iyzico'nun tuttuğu, PG'nin aracıladığı kart; mağaza
  yalnız çalışma-anında gösterilebilir izdüşümünü (marka, son 4 hane, son-kullanma, etiket, opak
  kart-handle) görür — kalıcı saklamaz.
- **Kart ekleme oturumu:** PG'den alınan hosted ekran linki + dönüş (callback) izi; sonucu kart kaydına
  (PG kullanıcı-handle) bağlar; tek-kullanımlık + süre-sınırlı.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Müşteri, kart eklemeyi (link alma → iyzico ekranında giriş → sohbette görünür olma) 3
  dakikanın altında tamamlayabilir.
- **SC-002**: Kart ekleme/listeleme/silme akışlarının hiçbirinde ham kart numarası veya CVV mağaza
  kaydında, sohbet transkriptinde veya MCP argümanında **hiç** görünmez (0 sızıntı — denetlenebilir).
- **SC-003**: Kayıtlı kart listesi, PG/iyzico'daki gerçek durumla %100 tutarlıdır (yerel bayat kopya
  kaynaklı tutarsızlık olamaz, çünkü canlı çekilir).
- **SC-004**: Sandbox test kartlarıyla ekle → listele → sil uçtan uca senaryosu tek oturumda başarıyla
  tamamlanır.
- **SC-005**: Bir kullanıcının kart işlemleri başka kullanıcının kartlarını hiçbir koşulda etkilemez.
- **SC-006**: Mağaza kod tabanında iyzico'ya doğrudan referans (paket/HTTP) taraması **sıfır** sonuç
  verir (izolasyon denetlenebilir).

## Assumptions

- Kart yönetimi **giriş yapmış** kullanıcıya özeldir (anonim kart saklama yok); müşteri yüzeyi
  mcp-gateway üzerinden upfront login modeliyle uyumludur.
- **iyzico erişimi PG'nin (harici DropShop repo) içindedir.** Mağaza (bu repo) yalnız PG ile REST
  konuşur; iyzico SDK/HTTP bu repoda yer almaz. Değiştirilebilir seam PG'de.
- iyzico hosted kart ekranı tarayıcıda açılır; Claude Desktop sohbet istemcisidir, ekranı kendi içinde
  render etmez — linki kullanıcı tarayıcıda açar.
- Standalone kart ekleme, PG'nin iyzico hosted ekranını nominal doğrulama işlemiyle açar (küçük auth,
  gerçek çekim değil); sandbox'ta para hareketi yoktur.
- Eski yerel kart deposu (mevcut `SavedCard` yerel entity + `ICardTokenizer` yerel-vault yolu) canlı-
  çekim + PG kullanıcı-handle modeliyle değiştirilir. `SavedCard` yerel kalıcılığı kalkar.
- Çekim NON-3D'dir: 3DS/SMS-OTP banka doğrulama ekranı **kullanılmaz**; yerini çekim öncesi agent-onayı
  (FR-014) tutar. Gerçek çekim PG içindeki iyzico NON-3D çağrısıyla yapılır.
- **PG'nin kart sözleşmesi bir bağımlılıktır:** add-session/list/delete/charge uçları PG'de mevcut ya
  da PG-içi iyzico faslında sağlanır (075 bu repo tarafını yazar, PG kontratını varsayar).
- **Merchant onboarding + MerchantKey = mevcut ön-koşul (070, `/mcp-admin`):** admin PG'ye onboard olup
  MerchantKey alır (`admin_submit_onboarding`/`_status`/`set_merchant_credentials`); `MerchantTokenProvider`
  bu key'le PG'ye auth eder — kart uçları bunu yeniden kullanır. MerchantKey MCP/agent'a çıkmaz (internal
  S2S). Bu akışın iyzico/PG'ye göre **düzenlenmesi ayrı bir sonraki spec'e bırakıldı** (075 kapsam-dışı;
  075 mevcut haliyle ön-koşul kabul eder).
- Adres defteri (`AddressBook`) bu feature'ın kapsamı dışındadır ve değişmez.
- Kapsam bu repo (mağaza tarafı); PG-içi iyzico entegrasyonu ayrı fasıl/ayrı repo.