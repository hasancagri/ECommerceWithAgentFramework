# Feature Specification: Hosted Merchant Onboarding + Ekrandan Credential Teslimi

**Feature Branch**: `078-hosted-onboarding-form`

**Created**: 2026-09-18

**Status**: Draft

**Input**: User description: "Hosted merchant onboarding formu: onboarding PII'sinin (TCKN/IBAN) ve
MerchantKey'in LLM sohbetinden geçmesini bitir. Admin agent yalnız PG tarafında hosted onboarding form
linki açtırır; müstakbel merchant bilgileri ekranda kendisi doldurur. PG Approved yaptığı anda başvuru
sahibine mail atar; mail içinde tek kullanımlık link olur ve o linkle PG'nin sitesi üzerinden MerchantId
ve MerchantKey görüntülenir/teslim edilir. ECommerce tarafında 'PG için merchant bilgilerimi güncelle'
denince ECommerce ekranı açılır; MerchantId ve MerchantKey oraya elle girilir. Mevcut chat-tabanlı
admin_submit_onboarding PII alanları sökülür. PG tarafındaki form/mail ayrı repo işi — bu spec store
(ECommerce) tarafını ve PG ile kontratı kapsar."

**Kademe**: Tam — yeni servisler-arası kontrat (store↔PG onboarding oturumu), store'a yeni hosted ekran
yüzeyi, mevcut MCP tool sözleşmesi değişiyor (PII/key alanları sökülüyor). Belirsizlik payı var.

## Sorun (bugünkü durum)

070'in onboarding zinciri tümüyle sohbetten akar: `admin_submit_onboarding` TCKN/IBAN dahil tüm PII'yi
LLM üzerinden alır; `admin_onboarding_status` Approved'da **MerchantKey'i sohbet yanıtında döndürür**;
`admin_set_merchant_credentials` key'i yine sohbetten yazdırır. PII ve uzun ömürlü gizli anahtar LLM
konuşma geçmişine (ve sağlayıcı tarafına) sızar. Bu, kayıtlı tasarım borcudur; bu feature borcu kapatır.

## Clarifications

### Session 2026-09-18

- Q: Store credential giriş ekranının erişim modeli? → A: B — agent tool'u süreli + tek kullanımlık
  imzalı link üretir; ekran linkle açılır, ayrıca login istemez (hosted ödeme sayfası emsali; store'a
  web-login yüzeyi geri eklenmez).
- Q: Store↔PG onboarding kontrat kanalı? → A: B — PG yeni S2S REST uçları sunar (oturum aç / durum);
  auth mevcut makine kimliğiyle (client_credentials, `ecommerce-onboarding` istemcisi, mevcut token
  handler'ı REST client'a takılır). Store'daki imperatif MCP sapması (`MerchantOnboardingClient`)
  SÖKÜLÜR; kontrat `contracts/`'ta REST olarak yazılır.
- Q: Credential girişinde anlık doğrulama? → A: B — store, kayıt anında PG'ye doğrulama çağrısı yapar;
  MerchantId+Key ikilisi anında sınanır, sonuç ekranda gösterilir (yanlış yapıştırma ilk ödemeye
  kalmaz). PG kontratına doğrulama ucu eklenir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - PII'siz başvuru başlatma (Priority: P1)

Store admin'i, admin agent'ına "PG'ye merchant kaydı başlat" der. Agent hiçbir kimlik/finans bilgisi
İSTEMEZ; tek çağrıyla PG tarafında hosted onboarding form linki üretilir ve sohbete yalnız o link düşer.
Müstakbel merchant linki açar, başvuru formunu PG'nin ekranında kendisi doldurur; başvuru PG'de
Pending doğar. PII store'a ve LLM'e hiç uğramaz.

**Why this priority**: Borcun ana gövdesi — PII'nin sohbetten geçişini kesen adım; teslim akışının
ön koşulu.

**Independent Test**: Admin agent'tan link alınır, formda örnek başvuru doldurulur; PG Admin ekranında
başvurunun Pending göründüğü ve sohbet transkriptinde hiçbir PII alanı geçmediği doğrulanır.

**Acceptance Scenarios**:

1. **Given** yetkili admin oturumu, **When** admin onboarding başlatmayı ister, **Then** tek tool
   çağrısıyla hosted form linki döner ve yanıtta PII alanı istenmez/yer almaz.
2. **Given** üretilmiş form linki, **When** müstakbel merchant formu doldurup gönderir, **Then**
   başvuru PG'de Pending durumda oluşur ve store yalnız durum bilgisini görebilir.
3. **Given** PG erişilemez, **When** admin link ister, **Then** teknik detay sızdırmayan dostane bir
   hata döner.

---

### User Story 2 - Tek kullanımlık credential teslimi (PG tarafı, kontrat) (Priority: P1)

PG Admin başvuruyu Approved yaptığı anda PG, başvuru e-postasına bir mail gönderir. Mailde **tek
kullanımlık, süreli** bir link vardır; link PG'nin sitesinde açılır ve MerchantId + MerchantKey **bir
kez** görüntülenir. İkinci açılışta/süre sonunda link ölüdür.

**Why this priority**: Key'in sohbete girmeden merchant'ın eline geçmesinin tek yolu; US3'ün girdisi.

**Independent Test**: PG Admin ekranından approve yapılır; Mailpit'te mailin düştüğü, linkin ilk
açılışta ikiliyi gösterdiği, ikinci açılışta reddettiği doğrulanır. (Uygulama PG repo'sunda; buradaki
doğrulama kontrat kabulüdür.)

**Acceptance Scenarios**:

1. **Given** Pending başvuru, **When** PG Admin approve eder, **Then** başvuru e-postasına tek
   kullanımlık linkli mail gider.
2. **Given** teslim linki ilk kez açılır, **When** sayfa yüklenir, **Then** MerchantId + MerchantKey
   bir kez gösterilir ve link tüketilmiş sayılır.
3. **Given** tüketilmiş ya da süresi geçmiş link, **When** tekrar açılır, **Then** bilgi gösterilmez.

---

### User Story 3 - Store ekranından credential girişi (Priority: P1)

Admin, agent'a "PG için merchant bilgilerimi güncelle" der. Agent sohbete yalnız store'un kendi
**credential giriş ekranının** linkini düşürür. Admin ekranı açar, PG sayfasından aldığı MerchantId +
MerchantKey'i oraya elle girer; store kaydeder ve mevcut ödeme akışları (MerchantKey S2S) yeni key ile
çalışır. Key sohbet transkriptine hiç girmez.

**Why this priority**: Teslimin store'a bağlanma adımı; bu olmadan zincir kapanmaz.

**Independent Test**: Agent'tan ekran linki alınır, ekranda örnek ikili kaydedilir; kaydın
MerchantInformation'a işlendiği ve hosted ödeme akışının yeni credentials ile çalıştığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** yetkili admin, **When** agent'tan güncelleme ekranı istenir, **Then** sohbete yalnız
   süreli/kişiye bağlı bir ekran linki düşer.
2. **Given** açılan ekran, **When** MerchantId + MerchantKey girilip kaydedilir, **Then** store
   ikiliyi PG'ye karşı anında doğrular, geçerliyse saklar; işlem izi (AdminActionLog) yazılır, key
   izde/logda YER ALMAZ. Geçersiz ikili reddedilir, ekran hatayı gösterir.
3. **Given** yetkisiz ya da süresi geçmiş ekran erişimi, **When** sayfa açılmak istenir, **Then**
   giriş reddedilir.
4. **Given** kayıt tamam, **When** hosted ödeme akışı çalıştırılır, **Then** yeni credentials ile
   ödeme linki üretilir.

---

### User Story 4 - PII'li sohbet yüzeyinin sökümü (Priority: P2)

Eski chat-tabanlı yüzey kaldırılır: `admin_submit_onboarding`'in PII alanları (link-üreten yeni davranış
lehine), `admin_onboarding_status` yanıtındaki MerchantKey alanı ve `admin_set_merchant_credentials`
sohbet-girişli tool'u sökülür. Agent bu bilgileri isteyecek bir yüzeye artık sahip değildir.

**Why this priority**: Söküm olmadan borç kapanmış sayılmaz; ama yeni yol canlı doğrulanmadan
sökülmemeli (sıralama).

**Independent Test**: `/mcp-admin` tool listesinde PII alan/key döndüren yüzey kalmadığı; status
sorgusunun yalnız durum + mesaj (+ret nedeni) döndürdüğü doğrulanır.

**Acceptance Scenarios**:

1. **Given** yeni akış canlıda doğrulanmış, **When** eski yüzey sökülür, **Then** `/mcp-admin`'de PII
   isteyen ya da MerchantKey döndüren hiçbir tool kalmaz.
2. **Given** admin durum sorar, **When** başvuru Approved, **Then** yanıt yalnız durum/yönlendirme
   içerir ("mailindeki linkten anahtarını al, store ekranından gir"), key içermez.

---

### Edge Cases

- Aynı e-posta ile ikinci başvuru başlatma: PG mevcut Pending başvuruyu döndürür ya da yenisini
  reddeder — kontratta tekilleştirme kuralı tanımlanır (başvuru kimliği = e-posta, 070 ile aynı).
- Form linki süresi dolmuş/hiç kullanılmamış: yeni link istenebilir; eski link ölür.
- Teslim maili kaybolur/silinir: PG Admin yeniden teslim linki üretebilir (yeni mail, eski link ölür) —
  PG tarafı yetenek; kontratta not edilir.
- Yanlış/eksik MerchantId-Key girişi: store kayıt anında PG'ye doğrulama çağrısı yapar; ikili
  geçersizse kayıt reddedilir ve ekran anında hata gösterir (FR-013). PG erişilemezse kayıt
  "doğrulanamadı" notuyla saklanır, ekran bunu söyler.
- Başvuru Rejected: durum sorgusu ret nedenini döndürmeye devam eder (bugünkü davranış korunur).
- Mevcut credentials varken yeniden giriş: ekran üzerine yazar (rotasyon yolu); iz yazılır.
- PG erişilemez: link üretimi ve durum sorgusu dostane hata verir (bugünkü davranış korunur).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Store admin yüzeyi, hiçbir PII almadan PG'de bir onboarding form oturumu başlatabilmeli;
  çıktı yalnız hosted form linkidir.
- **FR-002**: Onboarding formu PG tarafında barındırılır; PII (TCKN/IBAN vb.) store'a yazılmaz,
  store'dan geçmez, LLM sohbetine girmez.
- **FR-003**: Form linki süreli ve tek başvuruya bağlıdır; süresi dolan link kullanılamaz.
- **FR-004**: PG, başvuru Approved olduğunda başvuru e-postasına tek kullanımlık, süreli teslim linki
  içeren mail gönderir; link PG sitesinde MerchantId + MerchantKey'i BİR KEZ gösterir. (PG tarafı işi;
  kontrat bu spec'te.)
- **FR-005**: Store, admin'in credential girişi için kendi hosted ekranını sunar; ekran linki agent
  tool'undan alınır. Erişim modeli: süreli + TEK KULLANIMLIK imzalı link (hosted ödeme sayfası emsali);
  ekran ayrıca login istemez, web-login yüzeyi geri eklenmez. Linki yalnız yetkili admin (tool scope'u)
  üretebilir; ekran yazma-only'dir (mevcut key asla gösterilmez).
- **FR-006**: Ekrandan girilen MerchantId + MerchantKey store'da mevcut merchant-bilgisi kaydına
  işlenir; mevcut ödeme akışları (MerchantKey S2S) davranış değiştirmeden yeni değerlerle çalışır.
- **FR-007**: Credential kaydı AdminActionLog'a iz yazar; MerchantKey hiçbir izde/logda/sohbet
  yanıtında yer almaz. — SAPMA (2026-09-19, kullanıcı kararı): iz mekanizması (AdminActionLog)
  canlı PASS sonrası SÖKÜLDÜ; "key hiçbir yerde yer almaz" yarısı yürürlükte kalır.
- **FR-008**: `admin_onboarding_status` MerchantKey döndürmeyi bırakır; Approved yanıtı kullanıcıyı
  mail + store ekranı yoluna yönlendirir.
- **FR-009**: Eski PII'li `admin_submit_onboarding` alanları ve `admin_set_merchant_credentials`
  sohbet-girişli tool'u, yeni akış canlı doğrulandıktan sonra sökülür.
- **FR-010**: Store↔PG onboarding kanalı S2S REST'tir; makine kimliğiyle (client_credentials) auth
  olur, admin kullanıcı token'ı dış realm'e gitmez (bugünkü ilke korunur). Bugünkü imperatif MCP
  istemcisi (anayasa sapması) bu feature ile sökülür — sapmanın gerekçesi (PG'ye dokunma yasağı) kalktı.
- **FR-011**: PG erişilemezken tüm onboarding yüzeyleri teknik detay sızdırmayan dostane hata döner.
- **FR-012**: PG ile kontrat (REST: oturum açma + durum + credential doğrulama; teslim-linki
  semantiği, tekilleştirme) bu feature'ın `contracts/` klasöründe yazılı hale gelir; PG
  implementasyonu ayrı repo'da bu kontrata göre yapılır.
- **FR-013**: Store, ekrandan girilen MerchantId + MerchantKey'i kayıt anında PG'ye karşı doğrular;
  geçersiz ikili reddedilir ve ekranda anında hata gösterilir. PG o an erişilemezse kayıt
  "doğrulanamadı" işaretiyle saklanır ve ekran bunu belirtir.

### Key Entities

- **Onboarding Başvurusu (PG tarafı, kavramsal)**: e-posta kimlikli başvuru; Pending → Approved /
  Rejected yaşam döngüsü; approve'da teslim-linki üretir.
- **Form Oturumu / Form Linki (PG tarafı)**: store'un başlattığı, süreli, tek başvuruluk hosted form
  erişimi.
- **Teslim Linki (PG tarafı)**: tek kullanımlık, süreli; MerchantId + MerchantKey'i bir kez gösterir.
- **MerchantInformation (store, mevcut)**: MerchantId + MerchantKey'in saklandığı kayıt; bu feature
  yalnız doldurulma YOLUNU değiştirir (sohbet yerine ekran).
- **Credential Giriş Ekranı Oturumu (store)**: agent tool'unun ürettiği süreli + tek kullanımlık
  imzalı link; kullanım/süre sonunda ölür, ekran login istemez, yazma-only.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Uçtan uca onboarding (başvuru → approve → credential girişi) tamamlanırken TCKN, IBAN
  ve MerchantKey LLM sohbet transkriptinde **0 kez** geçer.
- **SC-002**: Admin, tek agent isteğiyle (tek tool çağrısı) form linkini ya da credential ekran
  linkini alır.
- **SC-003**: Teslim linki ikinci açılışta ya da süre sonunda bilgi göstermez (tek kullanımlık kuralı
  %100 uygulanır).
- **SC-004**: Credential girişi sonrası mevcut hosted ödeme akışı değişiklik gerektirmeden çalışır.
- **SC-005**: Söküm sonrası `/mcp-admin` yüzeyinde PII isteyen ya da MerchantKey döndüren tool sayısı
  sıfırdır.

## Assumptions

- Teslim maili başvuru e-postasına gider (e-posta = başvuru kimliği, 070 kuralı sürer); ayrıca admin'e
  kopya gerekmiyor.
- Form alan seti bugünkü sözleşmeyle aynıdır (type: Personal | PrivateCompany |
  LimitedOrJointStockCompany + koşullu TCKN/vergi alanları); doğrulama PG formundadır.
- PG Admin onay ekranı (PG Admin BFF) mevcuttur ve değişmez; approve'a mail tetiği eklenir (PG işi).
- Store'un "ekransız mağaza" duruşu bozulmaz: bu ekran müşteri yüzeyi değil, hosted ödeme sayfası
  emsalinde dar bir yönetim istisnasıdır; müşteri akışları MCP-only kalır.
- Link süreleri makul varsayılanla başlar (form linki ~24 saat, teslim + ekran linki ~1 saat);
  kesin değer plan aşamasında sabitlenir.
- Test sonrası sandbox MerchantKey rotate edilir (operasyonel not — mevcut memory kaydı).
- PG repo'su bu feature kapsamında AYRI çalışmayla güncellenir; bu repo'daki iş kontrat + store
  yüzeyleriyle sınırlıdır.