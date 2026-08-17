# Feature Specification: Müşteri Hizmetleri Tam-Sayfa Chat Ekranı

**Feature Branch**: `034-customer-service-chat-page`

**Created**: 2026-08-16

**Status**: Draft

**Input**: User description: "Müşteri hizmetleri tam-sayfa chat ekranı (WebApp-only). Mevcut global chat widget'ının
toggle paneli kaldırılır; icon /musteri-hizmetleri sayfasına yönlendirir. Yeni sayfa tam-yükseklik chat UI sunar;
mevcut chat akışını aynen kullanır. Amaç: uçtan uca e-ticaret akışını metin üzerinden geniş ekranda yürütmek.
Kalıcı geçmiş yok, anonim kimlik yok, backend değişikliği yok."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Metinle uçtan uca alışveriş (Priority: P1)

Giriş yapmış kullanıcı Müşteri Hizmetleri sayfasını açar; asistanla yazışarak ürün arar, sepete ekler,
sipariş verir ve kayıtlı kartıyla öder. Tüm süreç tek geniş chat ekranında yürür.

**Why this priority**: Feature'ın varlık sebebi; e-ticaret sürecinin metinle yönetilebilmesi.

**Independent Test**: Login'li kullanıcı sayfada "X ara → sepete ekle → sipariş ver → kayıtlı kartla öde"
zincirini yalnız yazışarak tamamlar.

**Acceptance Scenarios**:

1. **Given** login'li kullanıcı sayfada, **When** ürün aramak yazar, **Then** asistan sonuçları akan metinle listeler.
2. **Given** aynı oturumda süren yazışma, **When** "sepete ekle" der, **Then** asistan önceki bağlamı hatırlayıp ekler.
3. **Given** sepette ürün var, **When** sipariş + kayıtlı kartla ödeme ister, **Then** sipariş oluşur, sonuç mesajla bildirilir.

---

### User Story 2 - Icon'dan sayfaya geçiş (Priority: P2)

Herhangi bir sayfadaki sağ-alt chat icon'una tıklayan kullanıcı, açılır panel yerine Müşteri Hizmetleri
sayfasına yönlendirilir.

**Why this priority**: Giriş noktası; mevcut widget alışkanlığını yeni sayfaya taşır.

**Independent Test**: Ana sayfada icon'a tıkla; tarayıcı /musteri-hizmetleri adresine gider, panel açılmaz.

**Acceptance Scenarios**:

1. **Given** kullanıcı herhangi bir sayfada, **When** chat icon'una tıklar, **Then** Müşteri Hizmetleri sayfası açılır.
2. **Given** kullanıcı Müşteri Hizmetleri sayfasında, **When** sayfaya bakar, **Then** ikinci bir chat paneli/icon çakışması yoktur.

---

### User Story 3 - Anonim kullanıcı deneyimi (Priority: P3)

Giriş yapmamış kullanıcı sayfayı açabilir; asistanla yalnız ürün arama kapsamında yazışır ve girişe
yönlendiren bir bağlantı görür.

**Why this priority**: Sayfa herkese açık; anonim akış bozulmamalı ama tam deneyim login ister.

**Independent Test**: Çıkış yapmış tarayıcıda sayfayı aç; ürün arama yanıt verir, giriş bağlantısı görünür.

**Acceptance Scenarios**:

1. **Given** anonim kullanıcı sayfada, **When** ürün arar, **Then** asistan sonuç döner (genel kapsam).
2. **Given** anonim kullanıcı sayfada, **When** sayfaya bakar, **Then** giriş yapmadığı bilgisi ve giriş bağlantısı görünür.
3. **Given** anonim kullanıcı, **When** sepete ekleme/sipariş ister, **Then** asistan bunun giriş gerektirdiğini söyler.

---

### Edge Cases

- Yanıt akışı kesilirse (ağ hatası) kullanıcıya hata mesajı gösterilir; sayfa kilitlenmez, yeni mesaj yazılabilir.
- Sayfa yenilenince yazışma geçmişi kaybolur (kalıcı geçmiş kapsam dışı); bu bilinçli davranıştır.
- Asistan yanıt üretirken kullanıcı yeni mesaj gönderirse giriş alanı akış bitene dek devre dışıdır.
- Uzun yanıtlar mesaj listesini taşırmaz; liste kendi içinde kayar, sayfa düzeni bozulmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem /musteri-hizmetleri adresinde tam-yükseklik chat ekranı (mesaj listesi + giriş alanı) sunmalı.
- **FR-002**: Sağ-alt chat icon'u tüm sayfalarda kalmalı; tıklanınca panel açmak yerine bu sayfaya yönlendirmeli.
- **FR-003**: Mevcut açılır chat paneli kaldırılmalı; chat deneyimi yalnız yeni sayfada yaşamalı.
- **FR-004**: Sayfa, mevcut chat akışını (kimlik durumuna göre asistan/genel kapsam) davranış değişikliği olmadan kullanmalı.
- **FR-005**: Yanıtlar kullanıcıya akan metin (kelime kelime) olarak gösterilmeli.
- **FR-006**: Aynı sayfa oturumu içinde yazışma bağlamı korunmalı; asistan önceki mesajları hatırlamalı.
- **FR-007**: Anonim kullanıcıya giriş yapmadığı bilgisi ve mevcut giriş ekranına bağlantı gösterilmeli.
- **FR-008**: Akış hatasında kullanıcıya anlaşılır hata mesajı gösterilmeli; sayfa yeni mesaja izin vermeli.
- **FR-009**: Sunucu tarafında yeni veri saklama, kimlik mekanizması veya servis değişikliği yapılmamalı.
- **FR-010**: Header'a "Müşteri Hizmetleri" gezinme bağlantısı eklenmeli.

### Key Entities

Veri saklama yok; kalıcı entity doğmaz. Yazışma bağlamı yalnız oturum içi ve geçicidir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Login'li kullanıcı arama→sepet→sipariş→ödeme zincirini yalnız yazışarak, sayfadan ayrılmadan tamamlar.
- **SC-002**: Icon tıklaması %100 sayfaya yönlendirir; hiçbir sayfada açılır panel kalmaz.
- **SC-003**: Anonim kullanıcı ilk mesajına yanıt alır ve giriş bağlantısını görür.
- **SC-004**: Yanıt akışı başladıktan sonra metin kesintisiz akar; hata durumunda kullanıcı mesajla bilgilendirilir.

## Assumptions

- Mevcut chat altyapısı (kimlik durumuna göre kapsam ayrımı, akan yanıt) olduğu gibi yeniden kullanılır.
- Kalıcı yazışma geçmişi bilinçli kapsam dışı; sayfa yenilenince sıfırlanır.
- Anonim kimliklendirme (cihaz/telefon vb.) kapsam dışı; giriş mevcut ekrandan yapılır.
- Uçtan uca akışın gerektirdiği yetenekler (arama, sepet, sipariş, kayıtlı kartla ödeme) asistanda zaten mevcut.
- Tasarım dili mevcut WebApp görünümüyle uyumludur; ayrı tema çalışması yapılmaz.