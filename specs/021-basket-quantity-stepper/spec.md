# Feature Specification: Sepette Ürün Adedini Değiştirme (− / + Stepper)

**Feature Branch**: `021-basket-quantity-stepper`

**Created**: 2026-07-28

**Status**: Done

**Input**: User description: "Sepet sayfasında, seçilen ürün sayısını
değiştirebilmeliyim. En fazla 5 seçilebilsin; ürünün stok sayısı 3 ise en fazla 3."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Adedi artır/azalt (Priority: P1)

Kullanıcı sepet sayfasında bir ürün satırındaki eksi/artı butonlarıyla adedi
1 azaltıp artırabilir. Her tıklamada satır güncellenir, toplam fiyat yeni
adede göre yeniden hesaplanır.

**Why this priority**: Tek istenen davranış bu. Bugün sepette adet sabit bir
sayı; kullanıcı sepeti terk etmeden adet değiştiremiyor.

**Independent Test**: Sepete ürün ekle; + ile adedi 2'ye çıkar (toplam iki
katına çıkar), − ile 1'e indir; değişimlerin doğru yansıdığını doğrula.

**Acceptance Scenarios**:

1. **Given** sepette adedi 1 olan ürün, **When** + tıklanır,
   **Then** adet 2 olur ve toplam fiyat buna göre güncellenir.
2. **Given** sepette adedi 2 olan ürün, **When** − tıklanır,
   **Then** adet 1 olur ve toplam fiyat güncellenir.
3. **Given** adedi 1 olan ürün, **When** kullanıcı satıra bakar,
   **Then** − butonu devre dışıdır (adet 1'in altına inemez).

---

### User Story 2 - Üst sınır: min(5, stok) (Priority: P1)

Adet seçimi bir üst sınıra tabidir: **efektif max = min(5, kalan stok)**. Kullanıcı
bu sınıra ulaşınca + butonu devre dışı olur; sınırı aşan istekler reddedilir.

**Why this priority**: Kullanıcı isteği net — en fazla 5, ama stok azsa stok kadar.
US1 ile aynı slice'ta yaşar (aynı stepper), ayrı test edilebildiği için ayrıldı.

**Independent Test**: Stoğu 3 olan ürünü sepete al; + ile 3'e çıkar → + devre dışı.
Stoğu bol ürünü 5'e çıkar → + devre dışı. 5/stok üstü istek reddedilir.

**Acceptance Scenarios**:

1. **Given** stoğu ≥5 olan ürün, **When** adet 5'e ulaşır,
   **Then** + butonu devre dışıdır (5 üstüne çıkılamaz).
2. **Given** stoğu 3 olan ürün, **When** adet 3'e ulaşır,
   **Then** + butonu devre dışıdır (stok kadar seçilebildi).
3. **Given** herhangi bir istemci (UI/API/agent), **When** 5'ten büyük adet istenir,
   **Then** istek sunucuda reddedilir (sınır UI'a bağlı değildir).
4. **Given** stok sınırını aşan bir artış denemesi, **When** işlenir,
   **Then** reddedilir (fail-closed), adet değişmez, oversell olmaz.

---

### Edge Cases

- Adet 1 iken − devre dışıdır; ürünü çıkarmak yalnız mevcut **Remove** ile olur.
- Efektif max'a ulaşınca + devre dışıdır; sınır = min(5, kalan stok).
- Stok bilgisi bir işlemden sonra hafif bayatsa: + yanlışlıkla erken/geç devre dışı
  olabilir; her +/− işlemi tazeler ve sunucu fail-closed ile oversell'i yine önler.
- Adet değişimi rezervasyon süresine (geri sayım) **dokunmaz** — sayaç sıfırlanmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sepet satırı, adedi bir artıran ve bir azaltan iki kontrol (+ / −)
  SUNMALIDIR; her eylem satırın adedini günceller.
- **FR-002**: Adet değişince sepet satırı ve toplam fiyat yeni adede göre
  YENİDEN hesaplanıp gösterilmelidir.
- **FR-003**: Minimum adet 1'dir; adet 1 iken azaltma kontrolü DEVRE DIŞI olmalı,
  ürünü kaldırma yalnız mevcut Remove eylemiyle yapılmalıdır.
- **FR-004**: Bir satırın efektif üst sınırı **min(5, kalan stok)**'tur; adet bu
  sınıra ulaşınca artırma kontrolü DEVRE DIŞI olmalıdır.
- **FR-005**: Sabit üst sınır (5) sunucuda OTORİTER zorunlu kılınmalı — adedi
  artıran/güncelleyen her yol (UI, API, agent) 5'i aşan isteği reddetmelidir.
- **FR-006**: Stok sınırını aşan artış REDDEDİLMELİ (fail-closed), adet değişmemeli,
  oversell olmamalı; kullanıcıya anlaşılır bir hata gösterilmelidir.
- **FR-007**: Sepet, her satırın efektif üst sınırını (min(5, kalan stok)) arayüze
  BİLDİRMELİDİR ki artırma kontrolü doğru anda devre dışı kalsın.
- **FR-008**: Adet değişimi rezervasyon bitiş süresini (geri sayım) DEĞİŞTİRMEMELİDİR.
- **FR-009**: Adet değişimi yalnız kullanıcının kendi sepetini etkilemeli;
  yetkilendirme mevcut sepet-yazma kuralına uymalıdır.
- **FR-010**: Kapsam yalnızca sepet sayfasıdır; ödeme/sipariş akışı DEĞİŞMEZ.

### Key Entities *(include if feature involves data)*

- **Sepet Satırı (BasketItem)**: Bir ürünün sepetteki adı, fiyatı, **adedi** ve
  son işlemden bilinen **kalan serbest stoğu**. Efektif max = min(5, adet + kalan stok).
- **Stok (ProductStock, başka BC)**: Ürünün fiziksel stoğu ve aktif rezervasyonları;
  rezervasyon yanıtı kalan serbest stoğu (available) döndürür — sepet bunu saklar.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Kullanıcı sepet sayfasından ayrılmadan bir ürünün adedini
  artırıp azaltabilir; değişiklik ekranda tek eylemde görünür.
- **SC-002**: Adet değiştikçe toplam fiyat %100 tutarlı hesaplanır (adet × birim fiyat).
- **SC-003**: Adet hiçbir yolla min(5, stok) üstüne çıkamaz (oversell = 0, cap-ihlali = 0).
- **SC-004**: Adet değişimi geri sayımı hiçbir durumda sıfırlamaz.

## Assumptions

- Backend'de adet güncelleme mevcuttur (mutlak-değer, rezervasyonu stoğa aynalar,
  fail-closed); rezervasyon yanıtı kalan serbest stoğu (available) döndürür.
- Efektif üst sınır = min(5, adet + son bilinen kalan stok). "5" tek bir sunucu
  sabitidir; hem yazma reddi hem arayüz-sınırı bu sabitten türer.
- Kalan stok, her rezervasyon işleminde (ekle/güncelle) sepet satırında tazelenir;
  okuma anında ekstra stok sorgusu (cross-BC read) yapılmaz — bayatlık kabul edilir,
  fail-closed son korumadır.
- Rezervasyon çapası (017) ve süreli temizlik davranışı olduğu gibi korunur.
- MCP/agent'ın ayrı ekleme yolu adedi 1'e sıfırlar (5'i aşamaz), kapsam dışıdır.