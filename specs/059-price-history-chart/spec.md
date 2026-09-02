# Feature Specification: Ürün Detayında Fiyat Geçmişi

**Feature Branch**: `059-price-history-chart`

**Created**: 2026-09-02

**Status**: Draft

**Input**: User description: "Ürün detay sayfasında fiyat geçmişi grafiği. Catalog'da 058'den beri biriken append-only ProductPriceChange kayıtlarından ürünün fiyat geçmişini dönen anonim okuma query'si. WebApp ürün detay sayfasına kitapyurdu tarzı 'Fiyat Geçmişi' kutusu: harici chart kütüphanesi olmadan basit inline SVG çizgi grafik + kronolojik değişiklik listesi. Tek kayıt/kayıtsız üründe 'henüz fiyat değişmedi' metni."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Müşteri fiyat geçmişini görür (Priority: P1)

Ayşe bir kitabın detay sayfasına girer. Fiyat bilgisinin yakınında "Fiyat Geçmişi" kutusu görür: fiyatın zaman içindeki seyrini gösteren küçük bir çizgi grafik ve altında tarih + fiyat satırlarından oluşan değişiklik listesi. "Bu kitap geçen ay daha mı ucuzdu?" sorusuna tek bakışta cevap alır.

**Why this priority**: Programın (kitapyurdu hizası) ilk dilimi; kaynak veri 058'den beri birikiyor, tek eksik gösterim. Satın alma kararına doğrudan destek.

**Independent Test**: Fiyatı en az bir kez değişmiş bir ürünün detay sayfası açılarak test edilir; grafik + liste görünür, değerler kayıtlarla eşleşir.

**Acceptance Scenarios**:

1. **Given** fiyatı iki kez değişmiş bir ürün (3 kayıt), **When** müşteri detay sayfasını açar, **Then** kutuda 3 noktalı çizgi grafik ve 3 satırlık liste (tarih + fiyat) görünür.
2. **Given** aynı ürün, **When** liste incelenir, **Then** satırlar en yeni değişiklik üstte sıralıdır ve her satırda eski→yeni fiyat okunur (ilk kayıtta yalnız ilk fiyat).
3. **Given** giriş yapmamış (anonim) bir ziyaretçi, **When** detay sayfasını açar, **Then** fiyat geçmişi aynı şekilde görünür (login şartı yok).

---

### User Story 2 - Geçmişi olmayan ürün aldatmaz (Priority: P2)

Ali, fiyatı hiç değişmemiş (veya kayıt birikimi başlamadan eklenmiş) bir kitabın detayına girer. Boş bir grafik kutusu yerine kısa bir "henüz fiyat değişmedi" ifadesi görür; sayfa kalabalıklaşmaz, yanıltıcı boş grafik olmaz.

**Why this priority**: Mevcut katalogdaki kitapların çoğunda henüz geçmiş yok (birikim ileri dönük); ilk izlenimin bozuk görünmemesi gerekir.

**Independent Test**: Kayıtsız veya tek kayıtlı ürünün detay sayfası açılarak test edilir; grafik çizilmez, bilgi metni görünür.

**Acceptance Scenarios**:

1. **Given** hiç fiyat kaydı olmayan ürün, **When** detay sayfası açılır, **Then** grafik ve liste yerine "henüz fiyat değişmedi" metni görünür.
2. **Given** tek kaydı olan ürün (yalnız ilk fiyat), **When** detay sayfası açılır, **Then** grafik çizilmez; aynı bilgi metni görünür.

---

### Edge Cases

- Aynı gün birden çok değişiklik: hepsi ayrı satır olarak listelenir; grafik kayıt sırasıyla çizilir.
- Fiyat geçmişi verisi geçici alınamazsa: detay sayfası normal açılır, kutu hiç gösterilmez (sayfayı düşürmez).
- Çok uzun geçmiş: liste son 20 değişiklikle sınırlanır; grafik aynı pencereyi çizer.
- Yayından kaldırılan ürünün detayı zaten vitrinde görünmez; geçmiş için ek kural gerekmez.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, bir ürünün birikmiş fiyat değişim kayıtlarını (tarih, varsa eski fiyat, yeni fiyat) ürün detayında sunabilmelidir; erişim anonimdir.
- **FR-002**: İki veya daha çok kaydı olan üründe detay sayfası, fiyat seyrini kronolojik (soldan sağa eski→yeni) çizgi grafikle göstermelidir.
- **FR-003**: Detay sayfası, değişiklikleri en yeni üstte tarih + fiyat listesi olarak göstermelidir; ilk fiyat kaydı "ilk fiyat" olarak anlaşılır olmalıdır.
- **FR-004**: Sıfır veya tek kaydı olan üründe grafik ve liste yerine "henüz fiyat değişmedi" bilgi metni gösterilmelidir; boş grafik kutusu gösterilmez.
- **FR-005**: Fiyat geçmişi gösterimi yalnız mevcut birikmiş kayıtları okur; yeni kayıt, tablo, olay veya sözleşme üretmez.
- **FR-006**: Geçmiş verisi alınamazsa detay sayfası hatasız açılmalı, fiyat geçmişi kutusu görünmemelidir.

### Key Entities

- **Fiyat değişim kaydı**: Bir ürünün fiyatının belirli bir andaki değişimi — ürün, tarih, eski fiyat (ilk kayıtta yok), yeni fiyat. 058'den beri append-only birikir; bu feature yalnız okur.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Fiyatı değişmiş bir üründe müşteri, detay sayfasını açtığında ek tıklama olmadan fiyat seyrini görür (grafik + liste tek ekranda).
- **SC-002**: Geçmişi olmayan ürünlerin detay sayfalarında boş/yanıltıcı grafik alanı sıfırdır.
- **SC-003**: Fiyat geçmişi kutusu, detay sayfasının açılışını hissedilir yavaşlatmaz (sayfa mevcut hızında açılır).
- **SC-004**: Gösterilen her satır, yönetimde tutulan fiyat kayıtlarıyla birebir eşleşir (canlı doğrulamada örneklem kontrolü).

## Assumptions

- 058 öncesi eklenen kitaplarda geçmiş kaydı yok; kutu bu ürünlerde "henüz fiyat değişmedi" gösterir, birikim ileri dönük dolar (kabul edilmiş durum).
- Tek para birimi (₺); gösterim mevcut fiyat biçimlendirmesiyle aynıdır.
- Kayıtlar UTC tutulur; gösterimde yalnız tarih (gün) yeterlidir.
- Yönetici tarafındaki mevcut fiyat geçmişi listesi (058 admin düzenleme ekranı) bu feature'ın dışıdır ve değişmez.
- "Son X günün en düşük fiyatı" rozeti, indirim/kampanya gösterimi ve müşteri bildirimleri kapsam dışıdır (G2'nin sonraki dilimleri).
