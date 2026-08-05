# Feature Specification: Birleşik Profil Sayfası

**Feature Branch**: `027-unified-profile`

**Created**: 2026-08-05

**Status**: Draft

**Input**: User description: "Login sonrası header'daki ayrı My Addresses / My Cards / Order History linkleri tek 'Profilim' girişiyle değişsin; açılan sayfada üstte kullanıcının genel bilgileri (salt-okunur), altında sekmeli olarak adres / kart / sipariş bilgileri olsun."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tek "Profilim" girişinden kullanıcı bilgilerine ulaşma (Priority: P1)

Giriş yapmış kullanıcı, header menüsünde dağınık üç link yerine tek "Profilim"
girişi görür; tıklayınca genel bilgileri ile adres/kart/sipariş bölümlerinin
tümünü tek sayfada bulur.

**Why this priority**: Feature'ın çekirdeği. Bu olmadan diğer her şey anlamsız;
tek başına uygulanınca dahi kullanıcıya değer verir (dağınık menü toplanır).

**Independent Test**: Giriş yap, header'da tek "Profilim" gör, tıkla, tüm
bölümleri içeren profil sayfasına ulaş.

**Acceptance Scenarios**:

1. **Given** kullanıcı giriş yapmış, **When** header menüsünü açar, **Then**
   ayrı adres/kart/sipariş linkleri yok, tek "Profilim" girişi var (Sepetim ayrı kalır).
2. **Given** kullanıcı giriş yapmış, **When** "Profilim"e tıklar, **Then** genel
   bilgi + adres + kart + sipariş bölümlerini içeren profil sayfası açılır.
3. **Given** kullanıcı giriş yapmamış, **When** profil sayfası adresini açmayı
   dener, **Then** erişim reddedilir (giriş istenir).

---

### User Story 2 - Genel bilgilerini görme (Priority: P2)

Kullanıcı profil sayfasının üstünde kendi genel bilgilerini (ad, e-posta,
kullanıcı adı) salt-okunur olarak görür.

**Why this priority**: Kimliğini teyit ettiren, tanıdık e-ticaret hissi veren
bilgi; ama çekirdek toplama işi (P1) olmadan tek başına yeterli değil.

**Independent Test**: Profil sayfasını aç, üstte oturum bilgilerinden gelen
ad/e-posta/kullanıcı adının doğru gösterildiğini doğrula.

**Acceptance Scenarios**:

1. **Given** kullanıcı profil sayfasında, **When** sayfa yüklenir, **Then**
   oturumdaki ad/e-posta/kullanıcı adı üstte salt-okunur gösterilir.
2. **Given** bir bilgi oturumda yoksa, **When** sayfa yüklenir, **Then** o alan
   düzeni bozmadan boş/atlanmış gösterilir.

---

### User Story 3 - Sekmeler arası geçiş ve işlem sonrası aynı sekmede kalma (Priority: P2)

Kullanıcı adres/kart/sipariş sekmeleri arasında geçer; bir sekmede işlem
(ekle/düzenle/sil/varsayılan yap) yaptığında sayfa aynı sekmede kalır.

**Why this priority**: Mevcut işlevin (CRUD) kullanılabilir kalması için gerekli;
yanlış sekmeye düşmek kullanıcıyı şaşırtır.

**Independent Test**: Kartlarım sekmesinde bir kart sil, işlem sonrası hâlâ
Kartlarım sekmesinde olduğunu doğrula.

**Acceptance Scenarios**:

1. **Given** kullanıcı bir sekmede, **When** başka sekmeye tıklar, **Then**
   o sekmenin içeriği gösterilir.
2. **Given** kullanıcı bir sekmede işlem yapar, **When** işlem tamamlanır,
   **Then** sayfa aynı sekme açık şekilde geri gelir.
3. **Given** kullanıcının hiç adresi/kartı/siparişi yok, **When** ilgili sekmeyi
   açar, **Then** boş-durum mesajı gösterilir (hata değil).

---

### Edge Cases

- Doğrudan `?tab=<geçersiz>` ile gelinirse: varsayılan sekme (Adreslerim) açılır.
- Bir bölümün verisi yüklenirken servis hata dönerse: o sekmede hata mesajı,
  diğer sekmeler etkilenmez.
- Sayfa yer imine eklenip tekrar açılırsa: `?tab=` parametresindeki sekme açılır.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, giriş yapmış kullanıcıya header menüsünde ayrı adres/kart/
  sipariş linkleri yerine tek "Profilim" girişi göstermelidir.
- **FR-002**: "Sepetim" girişi ve geri sayım header'da olduğu gibi kalmalıdır.
- **FR-003**: Sistem, tek profil sayfasında kullanıcının adres, kart ve sipariş
  bilgilerini sekmeli olarak sunmalıdır.
- **FR-004**: Profil sayfası üstünde kullanıcının ad/e-posta/kullanıcı adı bilgisi
  oturumdan alınıp SALT-OKUNUR gösterilmelidir; bu bilgiler düzenlenemez.
- **FR-005**: Mevcut adres/kart/sipariş işlevleri (listeleme + ekle/düzenle/sil/
  varsayılan yap) profil sayfasında kayıpsız korunmalıdır.
- **FR-006**: Bir sekmede işlem yapıldıktan sonra kullanıcı aynı sekmede kalmalıdır.
- **FR-007**: Profil sayfası yalnızca giriş yapmış kullanıcılara açık olmalıdır;
  giriş yapmamış erişim reddedilmelidir.
- **FR-008**: Eski ayrı adres/kart/sipariş sayfaları erişilemez olmalı (kaldırılmalı);
  ortada bağlantısız (orphan) sayfa kalmamalıdır.

### Key Entities

Yeni veri varlığı yok. Gösterilen veriler mevcut kaynaklardan okunur:
- **Kullanıcı genel bilgisi**: ad, e-posta, kullanıcı adı — oturum kimliğinden.
- **Adres / Kart / Sipariş**: mevcut servislerin sağladığı hâliyle.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Giriş yapmış kullanıcı, tüm profil bilgilerine (genel + adres + kart
  + sipariş) header'dan tek tıkla ulaşır.
- **SC-002**: Header'da kullanıcıya özel adres/kart/sipariş için ayrı link sayısı
  3'ten 1'e (yalnız "Profilim") düşer.
- **SC-003**: Adres/kart/sipariş için önceden yapılabilen her işlem profil
  sayfasında da yapılabilir; işlev kaybı %0.
- **SC-004**: Bir sekmede işlem sonrası kullanıcı %100 aynı sekmede kalır.

## Assumptions

- Kullanıcının ad/e-posta/kullanıcı adı bilgisi oturum kimliğinde mevcuttur;
  ayrı bir profil servisi/çağrısı gerekmez.
- Genel bilgi düzenleme, şifre değiştirme, yeni backend ucu ve backend değişikliği
  kapsam dışıdır.
- Değişiklik yalnızca web arayüzü katmanındadır; servisler/veritabanları değişmez.
- Mevcut adres/kart/sipariş veri kaynakları olduğu gibi yeniden kullanılır.