# Feature Specification: Kategori ve Marka

**Feature Branch**: `016-category-brand`

**Created**: 2026-07-27

**Status**: Draft

**Input**: User description: "Sisteme Kategori ve Marka kavramlarını dahil et. Ürünler bir kategoriye ve bir
markaya ait olabilsin; yapı e-ticaret sitelerine yaklaşsın (ör. kategori/marka bazlı listeleme ve filtreleme).
Tedarikçi feed'i ve storefront gibi mevcut akışlarla ilişkisi netleştirilmeli. BrandType enum'ı kaldırılacak;
marka dinamik bir değer olacak."

**Kademe**: Tam — event kontratı değişir, birden çok servis etkilenir, belirsizlik var (anayasa: Artefakt Ölçekleme).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kategoriye göre listeleme ve filtreleme (Priority: P1)

Alışverişçi, ürün listesi sayfasında kategori seçerek yalnız o kategorideki ürünleri görür.

Kategori seçenekleri sistemde fiilen var olan kategorilerden oluşur; boş kategori seçeneği gösterilmez.

**Why this priority**: Feature'ın ana değeri; "e-ticarete yaklaşma" hedefinin görünür karşılığı budur.

**Independent Test**: Farklı kategorilerde ürünler varken listede kategori filtresi uygulanır; yalnız eşleşenler döner.

**Acceptance Scenarios**:

1. **Given** iki farklı kategoride ürünler, **When** alışverişçi bir kategori seçer, **Then** yalnız o kategorinin ürünleri listelenir.
2. **Given** kategori filtresi aktif, **When** sayfalama yapılır, **Then** filtre korunur ve sayfa sayısı filtreli sonuca göre hesaplanır.
3. **Given** hiç ürünü olmayan bir kategori değeri, **When** filtre seçenekleri gösterilir, **Then** o değer seçeneklerde yer almaz.

---

### User Story 2 - Markaya göre listeleme ve filtreleme (Priority: P1)

Alışverişçi, ürün listesini markaya göre filtreler; kategori ve marka filtresi birlikte de kullanılabilir.

Marka artık sabit bir liste değildir; sistemdeki markalar üründen/feed'den gelen gerçek değerlerdir.

**Why this priority**: Kategoriyle aynı kullanıcı değeri; marka zaten görünüyor ama filtrelenemiyor ve sabit listeye hapsolmuş.

**Independent Test**: Feed'de sabit listede olmayan yeni bir marka varken ürün sisteme girer ve markaya göre filtrelenebilir.

**Acceptance Scenarios**:

1. **Given** farklı markalarda ürünler, **When** alışverişçi bir marka seçer, **Then** yalnız o markanın ürünleri listelenir.
2. **Given** kategori + marka birlikte seçili, **When** liste yüklenir, **Then** iki koşulu da sağlayan ürünler döner.
3. **Given** feed'de bugüne dek görülmemiş bir marka adı, **When** ürün içeri alınır, **Then** ürün reddedilmez ve yeni marka filtrede görünür.

---

### User Story 3 - Tedarikçi feed'inden kategori/marka akışı (Priority: P2)

Tedarikçi feed'i her ürün için kategori bilgisi de taşır; içeri alım ürünle birlikte kategori ve markayı kataloğa yazar.

Değişiklik storefront listesine kendiliğinden yansır; elle bir adım gerekmez.

**Why this priority**: Feed, ürün verisinin giriş kapısıdır; kategori feed'den akmazsa filtreler dolmaz.

**Independent Test**: Feed'e kategorili bir ürün eklenir; içeri alım sonrası ürün storefront'ta doğru kategori/marka ile listelenir.

**Acceptance Scenarios**:

1. **Given** feed'de kategorisi olan yeni bir ürün, **When** içeri alım çalışır, **Then** ürün katalogda kategori ve marka bilgisiyle yer alır.
2. **Given** mevcut bir ürünün feed'de kategorisi değişti, **When** içeri alım çalışır, **Then** ürünün kategorisi güncellenir ve listeye yansır.
3. **Given** feed'de kategorisi boş/eksik bir ürün, **When** içeri alım çalışır, **Then** kayıt işlenmez (hata → retry/DLQ); kategori zorunludur.

---

### User Story 4 - Kategori/marka görünürlüğü ve asistan (Priority: P3)

Ürün detayında ve listelerde kategori/marka görünür; sohbet asistanı ürünleri kategori veya markaya göre arayabilir.

**Why this priority**: Tamamlayıcı görünürlük; ana filtreleme değeri US1/US2 ile zaten sağlanır.

**Independent Test**: Asistana "X kategorisindeki ürünleri göster" denir; yalnız o kategorinin ürünleri yanıtta yer alır.

**Acceptance Scenarios**:

1. **Given** kategorili bir ürün, **When** detay sayfası açılır, **Then** kategori ve marka bilgisi görünür.
2. **Given** asistan sohbeti, **When** kullanıcı kategori/marka bazlı arama ister, **Then** asistan filtreli sonuç döndürür.

---

### Edge Cases

- Feed'de kategori adı yazım farklarıyla gelirse ne olur (ör. "Elektronik" vs "elektronik ")? Normalizasyon kuralı gerekir.
- Mevcut (enum döneminden) ürünlerin marka değerleri yeni dinamik modele nasıl taşınır? Veri kaybı olmamalı.
- Kategorisiz ürün domain'de yoktur (FR-010): kategorisiz feed kaydı işlenmez; hata retry/DLQ'ya düşer, katalog kirlenmez.
- Bir kategorinin son ürünü silinirse/pasifleşirse filtre seçeneklerinden düşmeli.
- Catalog'un henüz raporlamadığı kısmi storefront satırları filtre seçeneklerine girmez; UI boş facet'i bozulmadan göstermeli.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Her ürün tam bir markaya sahiptir ve en fazla bir kategoriye aittir; ikisi de üründe görüntülenebilir olmalıdır.
- **FR-002**: Marka serbest (dinamik) bir değerdir; sabit/bilinen marka listesi zorunluluğu kaldırılır (BrandType enum'ı silinir).
- **FR-003**: Kategori düz tek seviyedir (hiyerarşi yok); her ürün en fazla bir kategori değeri taşır.
- **FR-004**: Kategori ve marka kimlikli kayıtlardır; yalnız feed'den içeri alımda kendiliğinden (get-or-create) doğar; yönetim (CRUD) ekranı yoktur.
- **FR-005**: Tedarikçi feed'i ürün başına kategori bilgisi taşır; içeri alım kategori ve markayı ürünle birlikte kataloğa yazar.
- **FR-006**: Servisler-arası ürün bildirimi kategori/marka için kimlik + görünen adı birlikte taşır; storefront görünümü ikisini de saklar.
- **FR-007**: Ürün listesi kategori ve/veya markaya göre filtrelenebilir (kimlikle veya adla); filtre sayfalama ile birlikte çalışır.
- **FR-008**: Filtre seçenekleri (mevcut kategori ve marka listeleri) gerçek verilerden kimlik+ad çifti olarak sorgulanabilir olmalıdır.
- **FR-009**: Kategori/marka adı normalize edilerek eşleştirilir (kırpma, iç boşluk toplama, harf duyarsız); normalize ad teklik anahtarıdır.
- **FR-010**: Kategori zorunludur (kullanıcı kararı 2026-07-27): kategorisiz ürün var olamaz; kategorisiz feed kaydı işlenmez (içeri alım hatası → retry/DLQ).
- **FR-011**: Mevcut ürünlerin marka verisi dinamik modele kayıpsız taşınır; geçiş sonrası eski ürünler markasıyla filtrelenebilir.
- **FR-012**: Sohbet asistanına açık ürün arama yetenekleri kategori ve marka ile daraltmayı destekler.
- **FR-013**: Kategori/marka adı kayıt doğduktan sonra değişmez (rename yok); yazım farkıyla gelen ad normalize eşleşmeyle mevcut kayda bağlanır.

### Key Entities

- **Kategori**: Kimlikli, düz (tek seviye) sınıflandırma kaydı; yalnız feed'den doğar, adı değişmez; filtre ve listelemede kullanılır.
- **Marka**: Kimlikli marka kaydı; yalnız feed'den doğar, adı değişmez; filtrelemede kullanılır.
- **Ürün (Katalog)**: Mevcut ürün kavramı; markasına ve kategorisine (ikisi de zorunlu) kimlikle referans verir.
- **Storefront görünümü**: Mevcut birleşik liste kaydı; kategori ve marka alanları kazanır, filtre sorguları buradan çalışır.
- **Tedarikçi ürün kaydı (feed/snapshot)**: Feed'deki ürün temsili; kategori alanı kazanır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Alışverişçi liste sayfasından tek etkileşimle kategori filtresi uygular ve yalnız eşleşen ürünleri görür.
- **SC-002**: Feed'e eklenen yeni kategorili/markalı ürün, bir içeri alım turu sonrasında elle müdahalesiz filtrelenebilir durumdadır.
- **SC-003**: Sabit marka listesinde olmayan bir marka içeren feed kaydı, hatasız içeri alınır ve markası filtrede görünür.
- **SC-004**: Geçiş sonrası mevcut ürünlerin %100'ü marka bilgisini korur; ana sayfa ve sayfalı liste davranışı değişmez.
- **SC-005**: Kategori + marka birlikte filtrelendiğinde sonuç ve sayfa sayısı tutarlıdır (yanlış sayfa/boş sayfa hatası yok).

## Assumptions

- Ürün tek kategoriye aittir; çoklu kategori (bir ürün birden çok kategoride) kapsam dışıdır.
- Filtreleme mevcut liste deneyimi üzerinde çalışır; ayrı bir "kategori sayfası" tasarımı zorunlu değildir.
- Feed maketi (Supplier.Api) bu feature kapsamında kategori alanı içerecek şekilde güncellenir; dış gerçek tedarikçi yoktur.
- Fiyat/stok/indirim akışları davranış değiştirmez; yalnız taşınan ürün bilgisi zenginleşir.
- Arama (ada göre) mevcut davranışını korur; kategori/marka daraltması ayrı filtre parametreleridir.
- Kategorisiz ürünler için ayrı bir "Kategorisiz" filtre seçeneği sunmak zorunlu değildir; filtresiz görünümde yer almaları yeterlidir.