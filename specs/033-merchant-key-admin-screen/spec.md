# Feature Specification: Admin MerchantKey Ekranı

**Feature Branch**: `feature/customer-gateway-vault-client`

**Created**: 2026-08-11

**Status**: Draft

**Input**: User description: "Admin MerchantKey ekranı: /Admin/Onboarding sayfasına Merchant Kimliği bölümü — admin, DropShop onayı
sonrası aldığı merchantId + MerchantKey'i girer; değer kalıcı saklanır; mevcut kaydın varlığı gösterilir (key asla gösterilmez);
WebApp'teki tüketicisiz bellek-içi key deposu ve ucu silinir."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Merchant kimliğini kaydet (Priority: P1)

Admin, DropShop onayı sonrası aktivasyon sayfasında bir kez gösterilen merchantId + MerchantKey'i
yönetim ekranına girer; değer kalıcı saklanır ve kart saklama (vault) bu kimlikle çalışır hale gelir.

**Why this priority**: Key kaydedilmeden kart saklama akışı fail-closed; feature'ın varlık sebebi bu.

**Independent Test**: Onaylı bir merchant kimliğiyle form doldurulur; kayıt sonrası kart ekleme
akışının vault tokenize çağrısı başarılı döner.

**Acceptance Scenarios**:

1. **Given** kayıtlı merchant kimliği yok, **When** admin geçerli merchantId + MerchantKey girer,
   **Then** değer kalıcı kaydedilir ve ekranda başarı mesajı görünür.
2. **Given** aynı merchantId kayıtlı, **When** admin yeni bir key girer, **Then** key güncellenir (upsert).
3. **Given** farklı merchantId kayıtlı (re-onboard), **When** admin yeni kimlik girer, **Then** eski kayıt
   yenisiyle değiştirilir.
4. **Given** boş merchantId veya boş key, **When** form gönderilir, **Then** kayıt yapılmaz, hata mesajı görünür.

---

### User Story 2 - Mevcut kaydı gör (Priority: P2)

Admin, ekranı açtığında kayıtlı merchant kimliğinin olup olmadığını, varsa hangi merchantId'nin ve en son
ne zaman güncellendiğini görür. Key hiçbir biçimde geri gösterilmez.

**Why this priority**: Kaydın tuttuğunun doğrulaması; yanlış/eksik kayıt teşhisi. Yazma olmadan da değer taşır.

**Independent Test**: Kayıt varken sayfa açılır; merchantId + güncelleme zamanı görünür, key alanı boş gelir.

**Acceptance Scenarios**:

1. **Given** kayıt yok, **When** admin sayfayı açar, **Then** "kayıtlı merchant kimliği yok" durumu görünür.
2. **Given** kayıt var, **When** admin sayfayı açar, **Then** merchantId + güncelleme zamanı görünür; key görünmez.

---

### User Story 3 - Yetki sınırı (Priority: P3)

Merchant kimliği yalnız admin tarafından okunur/yazılır; müşteri rolü veya anonim erişim reddedilir.

**Why this priority**: Key = ödeme gateway'i client_secret'ı; sızması tüm vault'u açar. Mevcut guard'ların teyidi.

**Independent Test**: Customer rolüyle ve anonim olarak ekran + arka uç uçlarına erişim denenir; hepsi reddedilir.

**Acceptance Scenarios**:

1. **Given** customer rollü kullanıcı, **When** yönetim ekranını açmayı dener, **Then** erişim reddedilir.
2. **Given** customer token'ı, **When** okuma/yazma ucu doğrudan çağrılır, **Then** yetki hatası döner.

---

### Edge Cases

- Arka uç erişilemezken form gönderilirse: kayıt yapılmaz, ekranda anlaşılır hata; teknik ayrıntı sızmaz.
- Key baştaki/sondaki boşlukla yapıştırılırsa: kaydedilmeden önce kırpılır.
- Kayıt sonrası sayfa yenilenirse: form boş, durum bölümü güncel kaydı gösterir (key asla geri dolmaz).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Yönetim ekranındaki onboarding sayfası, merchantId + MerchantKey giriş formu içeren
  "Merchant Kimliği" bölümü sunmalı.
- **FR-002**: Kaydetme, değeri kalıcı depoya upsert etmeli: aynı merchant → key güncelle; farklı/yok → yeni kayıt.
- **FR-003**: Ekran, mevcut kaydın varlığını, merchantId'sini ve son güncelleme zamanını göstermeli.
- **FR-004**: MerchantKey kaydedildikten sonra hiçbir ekranda/yanıtta geri gösterilmemeli.
- **FR-005**: Boş merchantId veya boş key reddedilmeli; key kaydedilmeden önce kırpılmalı (trim).
- **FR-006**: Okuma ve yazma yalnız admin yetki demetiyle yapılabilmeli; müşteri/anonim erişim reddedilmeli.
- **FR-007**: WebApp'teki tüketicisiz bellek-içi merchant key deposu ve ilgili uç kaldırılmalı; tek doğruluk
  kaynağı kalıcı kayıt olmalı.
- **FR-008**: Arka uç hatasında ekran anlaşılır bir hata mesajı göstermeli; işlem yapılmamış sayılmalı.

### Key Entities

- **Merchant Kimliği**: Mağazanın ödeme gateway'indeki kimliği; merchantId (tekil) + MerchantKey (gizli değer).
  Sistemde tek kayıt yaşar; güncelleme zamanı izlenir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin, onaydan gelen kimliği tek form gönderimiyle kaydedebilir; başarı mesajını 5 sn içinde görür.
- **SC-002**: Kayıt sonrası kart saklama akışı ek yapılandırma gerektirmeden çalışır (vault tokenize başarılı).
- **SC-003**: Kayıtlı key, uygulamanın hiçbir ekranında/yanıtında görünmez (durum yalnız varlık + merchantId + zaman).
- **SC-004**: Uygulama yeniden başlatıldığında kayıt kaybolmaz; ekran aynı durumu göstermeye devam eder.
- **SC-005**: Admin olmayan tüm erişim denemeleri (ekran + arka uç) reddedilir.

## Assumptions

- Merchant kimliği tekil kayıttır (tek gateway, tek mağaza); çoklu merchant kapsam dışı.
- Yazma için mevcut kalıcı upsert davranışı (aynı merchant → güncelle, farklı → değiştir) yeniden kullanılır.
- Okuma için admin yazma yetkisi yeterlidir; ayrı okuma yetkisi tanımlanmaz (okuma da yalnız admin işi).
- Key doğrulaması biçimsel değildir (boş/boşluk dışında); geçerliliği gateway'e ilk çağrıda anlaşılır.
- Ekran dili Türkçe'dir (mevcut yönetim ekranlarıyla tutarlı).
