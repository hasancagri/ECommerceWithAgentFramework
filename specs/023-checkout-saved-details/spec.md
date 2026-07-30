# Feature Specification: Checkout'ta Kayıtlı Adres + Kart Seçimi

**Feature Branch**: `023-checkout-saved-details`

**Created**: 2026-07-30

**Status**: Draft

**Input**: User description: "Checkout (Order/Create) sayfasında adres ve kart bilgisi sıfırdan
girilmez; kullanıcı yalnızca kayıtlı adres defterinden bir adres ve cüzdanından bir kayıtlı kart
SEÇER. Varsayılanlar otomatik seçili. Kayıtlı yoksa bloke + 'önce ekle' yönlendirmesi. Kapsam yalnız
WebApp Order/Create; backend/kontrat değişmez."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kayıtlı adres + kart seçerek sipariş ver (Priority: P1) 🎯 MVP

Giriş yapmış kullanıcı checkout sayfasında, daha önce kaydettiği adreslerden birini ve kayıtlı
kartlarından birini seçerek siparişi tamamlar. Adres/kart bilgisini elle girmez.

**Why this priority**: Checkout'un ana değeri; tekrar veri girişini kaldırır, sipariş akışını hızlandırır.

**Independent Test**: En az 1 kayıtlı adres + 1 kayıtlı kartı olan kullanıcıyla; checkout'a gir →
listelerden birer seçim yap → siparişi tamamla → sipariş oluşur.

**Acceptance Scenarios**:

1. **Given** kullanıcının ≥1 kayıtlı adresi ve ≥1 kayıtlı kartı var, **When** checkout sayfası açılır,
   **Then** kayıtlı adresler ve kartlar seçilebilir liste olarak görünür; elle giriş formu yoktur.
2. **Given** listelerden bir adres ve bir kart seçili, **When** kullanıcı siparişi onaylar,
   **Then** seçili adres siparişe kopyalanır, ödeme sepet tutarıyla yapılır ve sipariş oluşur.
3. **Given** adres veya kart seçilmemiş, **When** kullanıcı onaylamaya çalışır,
   **Then** sipariş oluşmaz ve seçim zorunluluğu bildirilir.

---

### User Story 2 - Varsayılan adres/kart otomatik seçili (Priority: P2)

Checkout açıldığında kullanıcının varsayılan adresi ve varsayılan kartı (varsa) önceden seçili gelir;
kullanıcı tek onayla ilerleyebilir.

**Why this priority**: Sık kullanılan yol için ekstra tık kaldırır; US1'in üzerine konfor katmanı.

**Independent Test**: Varsayılanı olan kullanıcıyla checkout'a gir → varsayılan adres+kart seçili gelir
→ değiştirmeden onayla → sipariş oluşur.

**Acceptance Scenarios**:

1. **Given** kullanıcının varsayılan adresi + varsayılan kartı var, **When** checkout açılır,
   **Then** o adres ve o kart önceden seçili gelir.
2. **Given** varsayılan yok ama ≥1 kayıt var, **When** checkout açılır,
   **Then** hiçbiri önseçili değildir; kullanıcı manuel seçer (US1 kural 3 geçerli).

---

### User Story 3 - Kayıtlı adres/kart yoksa yönlendir (Priority: P2)

Kullanıcının hiç kayıtlı adresi VEYA kartı yoksa checkout bloke olur; eksik olanı eklemesi için
"My Addresses" / "My Cards" sayfalarına yönlendiren bir mesaj gösterilir.

**Why this priority**: US1'in ön koşulu; boş durum ele alınmazsa akış çıkmaza girer.

**Independent Test**: Kayıtlı adresi/kartı olmayan kullanıcıyla checkout'a gir → sipariş formu yerine
"önce ekle" mesajı + ilgili sayfa linkleri görünür; sipariş verilemez.

**Acceptance Scenarios**:

1. **Given** kullanıcının hiç kayıtlı adresi yok, **When** checkout açılır,
   **Then** adres ekleme yönlendirmesi (My Addresses linki) gösterilir ve sipariş engellenir.
2. **Given** kullanıcının hiç kayıtlı kartı yok, **When** checkout açılır,
   **Then** kart ekleme yönlendirmesi (My Cards linki) gösterilir ve sipariş engellenir.

---

### Edge Cases

- Sepet boşsa VEYA rezervasyon süresi dolmuşsa checkout'a girilemez → sepete yönlendirilir (FR-009).
- Kullanıcı seçim yaptıktan sonra o kayıt başka sekmede silinirse: onayda seçim çözülemez → sipariş
  oluşmaz, kullanıcıdan yeniden seçim istenir.
- Kayıtlı adres/kart listeleri yüklenemezse (okuma hatası): checkout hata durumu gösterir, sipariş verilemez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Checkout, kullanıcının kayıtlı adreslerini ve kayıtlı kartlarını seçilebilir liste olarak
  göstermeli; adres/kart için elle giriş formu **bulunmamalı**.
- **FR-002**: Kart listesi hassas veri göstermemeli — yalnız marka + son 4 hane + son-kullanma + etiket.
- **FR-003**: Kullanıcı siparişi tamamlamak için bir adres ve bir kart **seçmiş olmalı**; eksikse sipariş
  engellenmeli ve seçim zorunluluğu bildirilmeli.
- **FR-004**: Varsayılan adres ve varsayılan kart (varsa) checkout açılışında önceden seçili gelmeli.
- **FR-005**: Kullanıcının hiç kayıtlı adresi **veya** kartı yoksa checkout engellenmeli ve eksik olanı
  eklemesi için ilgili yönetim sayfasına yönlendiren mesaj gösterilmeli.
- **FR-006**: Seçilen adres, siparişin teslimat adresi olarak kullanılmalı (mevcut sipariş kaydına).
- **FR-007**: Ödeme, sepet tutarıyla gerçekleştirilmeli; kullanıcıdan kart numarası/CVV **istenmemeli**.
- **FR-008**: Kapsam yalnız checkout sayfası deneyimidir; sipariş/ödeme/kayıtlı-veri iş kuralları ve
  sözleşmeleri bu feature'da **değişmemeli**.
- **FR-009**: Sepet boş veya rezervasyon süresi dolmuşsa checkout sayfasına **girilememeli**; kullanıcı
  sepete yönlendirilmeli ve durum bildirilmeli (GET ve POST'ta).

### Key Entities

- **Kayıtlı Adres (seçim)**: Kullanıcının adres defterindeki bir kayıt; il/ilçe/sokak/posta/satır +
  varsayılan işareti. Checkout yalnız okur ve seçilirse siparişe kopyalar.
- **Kayıtlı Kart (seçim)**: Kullanıcının cüzdanındaki bir kayıt; marka + son4 + son-kullanma + etiket +
  varsayılan işareti (PAN/CVV yok). Checkout yalnız okur ve gösterir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kullanıcı, kayıtlı adres+kartı varken checkout'u **elle hiçbir adres/kart alanı yazmadan**
  tamamlayabilir.
- **SC-002**: Varsayılanı olan kullanıcı, checkout'ta hiçbir seçim değiştirmeden tek onayla sipariş verebilir.
- **SC-003**: Checkout'un hiçbir ekranında ham kart numarası veya CVV gösterilmez/istenmez.
- **SC-004**: Kayıtlı adresi veya kartı olmayan kullanıcı, ne yapması gerektiğini (ekleme linki) tek
  bakışta anlar ve yanlışlıkla eksik sipariş veremez.

## Assumptions

- Kullanıcı giriş yapmıştır; kayıtlı adres/kart okuması kullanıcının kendi verisiyle sınırlıdır (022).
- 022 Wallet + AddressBook feature'ı canlıdır; okuma uçları (adres/kart listesi) mevcuttur.
- Ödeme akışı kart alanlarını fiilen kullanmaz (yalnız tutar); bu nedenle kayıtlı kartta PAN olmaması
  siparişi engellemez. Gerçek kartla-çekim gelecekteki PaymentGateway işine bırakılmıştır.
- Snapshot/immutability (siparişin kayıttan kopyayı dondurması) backend'i bu feature'ın parçası değildir;
  022 US3 kontratının yalnız WebApp checkout tarafıdır.
- Sepet ve sipariş oluşturma mevcut davranışını korur; yalnız adres/kart giriş yöntemi değişir.