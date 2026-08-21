# Feature Specification: Ürün Özellikleri ve Facet Filtre (Specifications)

**Feature Branch**: `043-product-specifications`

**Created**: 2026-08-21

**Status**: Draft

**Input**: User description: "Catalog Specifications — facet filtre (nopCommerce extract #6).
Kanonik özellikler tedarikçi feed'inden akar; vitrin soldan filtreler; detay spec tablosu gösterir;
insan eli ürüne değmez. Tasarım oturumu 2026-08-21 kararları geçerli."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Vitrinde özellik filtresi (Priority: P1)

Müşteri ürün listesinde sol panelden özellik değerleri seçer (ör. Renk: Siyah, Materyal: Çelik);
liste yalnız eşleşen ürünlere daralır. Mevcut kategori/marka filtreleri ve sayfalama ile birlikte
çalışır.

**Why this priority**: Feature'ın müşteri değeri bu ekranda doğar; filtresiz büyük katalog gezilmez.

**Independent Test**: Özellik ataması olan ürünler hazırlanır (seed/elle); panelde değer seçilir,
listenin doğru daraldığı ve sayfalamanın korunduğu doğrulanır — feed akışı olmadan da test edilir.

**Acceptance Scenarios**:

1. **Given** özellikli ürünler, **When** liste sayfası açılır, **Then** sol panelde filtrelenebilir
   özellikler ve değerleri görünür (yalnız yayında olan ürünlerden türetilmiş).
2. **Given** panel açık, **When** "Renk: Siyah" seçilir, **Then** listede yalnız siyah ürünler kalır;
   sayfalama ve mevcut kategori/marka seçimi korunur.
3. **Given** "Renk: Siyah" seçili, **When** "Renk: Beyaz" de seçilir, **Then** aynı özellik içinde
   seçimler GENİŞLETİR (siyah VEYA beyaz).
4. **Given** "Renk: Siyah" seçili, **When** "Materyal: Çelik" de seçilir, **Then** özellikler arası
   seçim DARALTIR (siyah VE çelik).
5. **Given** filtre kombinasyonu hiçbir ürünle eşleşmiyor, **When** liste yenilenir, **Then** boş
   sonuç durumu gösterilir; filtreler temizlenebilir.
6. **Given** özelliği hiç olmayan ürünler, **When** filtre SEÇİLİ DEĞİLKEN listelenir, **Then**
   listede normal görünürler (özelliksizlik ürünü vitrinden düşürmez).

---

### User Story 2 - Ürün detayında özellik tablosu (Priority: P2)

Müşteri ürün detay sayfasında ürünün özelliklerini (Renk, Materyal...) sıralı bir tabloda görür.

**Why this priority**: Filtrede seçilen değerin üründe görünür olması güven verir; ucuz tamamlayıcı.

**Independent Test**: Özellikli bir ürünün detay sayfası açılır; atanan tüm özellikler tanım
sırasına göre listelenir. Özelliksiz üründe bölüm hiç görünmez.

**Acceptance Scenarios**:

1. **Given** özellikleri atanmış ürün, **When** detay açılır, **Then** özellik adı + değeri satırları
   tanımlı sırayla görünür.
2. **Given** özelliksiz ürün, **When** detay açılır, **Then** özellik bölümü görünmez; sayfa normal.

---

### User Story 3 - Özellik verisi feed'den kendiliğinden akar (Priority: P3)

Tedarikçi feed'i ürün satırlarında özellik değerleri taşır; sistem bunları kanonik tanımlara eşler,
eksik kalanları yapay zekâ kapalı listeden seçerek tamamlar; sonuç vitrine kendiliğinden yansır.
Hiçbir insan ürünü elle düzenlemez.

**Why this priority**: Kalıcı veri kaynağı; P1/P2 elle hazırlanmış veriyle de yaşar, bu akış
sistemi kendi kendine besler hale getirir.

**Independent Test**: Feed rev dosyasına özellik değerleri eklenir; çekim sonrası ürünün kanonik
özellikleri vitrinde ve facet'te görünür. Eşlenemeyen/eksik değerli satırda AI kapalı listeden
tamamlar; liste-dışı hiçbir değer sisteme giremez.

**Acceptance Scenarios**:

1. **Given** feed satırında tanınan özellik değeri, **When** çekim + yayın gerçekleşir, **Then**
   ürünün kanonik özelliği vitrinde görünür; facet listesi güncellenir.
2. **Given** iki tedarikçi aynı ürüne farklı özellik seti veriyor, **When** birleştirme çalışır,
   **Then** özellik başına öncelikli tedarikçinin dolu değeri kazanır (sıra-bağımsız).
3. **Given** feed'de özellik alanı eksik/eşlenemiyor, **When** zenginleştirme çalışır, **Then** AI
   yalnız kapalı listeden değer seçer; seçemezse ürün ÖZELLİKSİZ yayınlanmaya devam eder.
4. **Given** AI liste-dışı bir değer üretti, **When** atama denenir, **Then** değer reddedilir ve
   ürün o özellik olmadan ilerler; akış durmaz.
5. **Given** özellikleri eksik ürün, **When** yayın koşulları değerlendirilir, **Then** özellik
   eksikliği yayını ENGELLEMEZ (mevcut eksiksizlik kuralı genişletilmez).

---

### Edge Cases

- Filtrelenebilir hiç özellik yoksa (veri birikmemiş) sol panelde özellik bölümü hiç görünmez.
- Seçili filtre değerini taşıyan son ürün yayından düşerse liste boş sonuç durumuna düşer; facet
  sonraki türetmede değeri artık listelemez.
- Aynı feed satırında bilinmeyen özellik ANAHTARI (eşleme tablosunda yok) → o anahtar yok sayılır;
  satırın diğer alanları normal işlenir.
- Kanonik tanım listesi (registry) iki tarafta da tohumlanır; tanımlar arası uyumsuzluk çıkmaması
  için sözleşme AD üzerindendir ve tanımlar yalnız tohumla değişir.
- Filtre seçimi URL'de taşınır (paylaşılabilir/yenilenebilir liste görünümü).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem kanonik özellik tanımlarını (ad, filtrelenebilirlik, sunum sırası) ve her
  tanımın kapalı değer listesini tohum veriyle kurmalı; tanımlar çalışma anında feed'den DOĞMAMALI.
- **FR-002**: Tedarikçi feed satırları opsiyonel özellik değerleri taşıyabilmeli; tanınan değerler
  kanonik tanımlara tedarikçi-başına eşleme kurallarıyla çevrilmeli.
- **FR-003**: Aynı ürüne birden çok tedarikçi özellik verdiğinde birleştirme özellik başına
  yapılmalı: öncelikli tedarikçinin dolu değeri kazanmalı, sonuç işleme sırasından bağımsız olmalı.
- **FR-004**: Özellik değeri eksik/eşlenemeyen üründe yapay zekâ tamamlaması yalnız kapalı listeden
  seçim yapabilmeli; liste-dışı değer üretimi reddedilmeli ve akışı durdurmamalı.
- **FR-005**: Özellik eksikliği ürünün yayınlanmasını engellememeli (mevcut eksiksizlik kuralı
  özelliklerle genişletilmez).
- **FR-006**: Yayınlanan ürün değişiklikleri kanonik özellik listesini (özellik adı + değer adı)
  vitrin tarafına taşımalı; vitrin satırı bu listeyi sorgulanabilir tutmalı.
- **FR-007**: Ürün listesi ekranı, yayında olan ürünlerden türetilmiş filtrelenebilir özellik +
  değer listesini (facet) sunmalı; değer yanında o değeri taşıyan ürün sayısı gösterilmeli.
- **FR-008**: Filtre seçimi aynı özellik içinde VEYA, özellikler arasında VE mantığıyla daraltmalı;
  kategori/marka filtreleri ve sayfalama ile birlikte çalışmalı; seçim URL'de taşınmalı.
- **FR-009**: Ürün detay sayfası atanmış özellikleri tanım sırasına göre listelemeli; özelliksiz
  üründe bölüm gizlenmeli.
- **FR-010**: Özellik tanımları en az okuma ucuyla dışa açılmalı (tanım + değer listesi
  sorgulanabilir); tanım yönetimi tohum verisiyle sınırlı kalmalı (yönetim ekranı yok).
- **FR-011**: Filtrelenebilir olmayan özellikler facet'te görünmemeli ama ürün detayında
  listelenebilmeli.

### Key Entities

- **SpecificationAttribute (kanonik tanım)**: ad, filtrelenebilirlik, sunum sırası + kapalı değer
  (option) listesi. Tohumla doğar; iki tarafta ayrı tohumlanır, sözleşme AD'dır.
- **Tedarikçi özellik eşlemesi**: tedarikçi-başına ham anahtar/değer → kanonik tanım/değer kuralı;
  tohumla doğar (kategori eşleme tablolarının ikizi).
- **Ürün özellik ataması**: ürün ↔ kanonik tanım + seçilmiş değer; ürünün parçası olarak yaşar.
- **Vitrin özellik listesi**: vitrin satırında sorgulanabilir (özellik adı, değer adı) çiftleri;
  facet ve filtre bu listeden türetilir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Bir özellik değeri seçildiğinde liste yalnız eşleşen ürünleri gösterir; yanlış
  pozitif/negatif oranı %0 (elle sayımla doğrulanabilir).
- **SC-002**: Filtreli liste sorgusu kullanıcıya 1 saniyenin altında yanıt verir.
- **SC-003**: Feed'e eklenen tanınan bir özellik değeri, bir çekim turu sonrasında vitrin
  facet'inde ve ürün detayında görünür.
- **SC-004**: Yapay zekâ tamamlaması hiçbir zaman kapalı liste dışında değer yazamaz (%0 liste-dışı
  atama).
- **SC-005**: Özelliksiz ürünlerin yayın durumu değişmez (%0 yayın regresyonu).
- **SC-006**: Facet'te görünen her değerin ürün sayısı, o filtre uygulandığında dönen ürün
  sayısıyla birebir eşittir.

## Assumptions

- Karar (tasarım oturumu, 2026-08-21): kaynak hibrit — feed `attributes` alanı taşır (mock
  dataset JSON'larına elle eklenir; kod-içi üretici yok), eksik EnrichmentAgent'la tamamlanır.
- Karar: eşleme Procurement'ta yapılır (tedarikçi-başına seed'li eşleme tabloları); kanonik yayın
  temiz özellik listesi taşır; Catalog'a tedarikçi farkındalığı sızmaz.
- Karar: filtre anahtarı kanonik AD'dır (özellik adı + değer adı); registry seed'li ve stabildir.
- Karar: atama Catalog'da Product'ın parçasıdır (ayrı aggregate değil — kategori ataması emsali);
  `SpecificationAttribute` ayrı seed'li aggregate'tir, REST penceresi kuralına uyar.
- Karar: vitrin tarafında özellikler StorefrontView satırına denormalize edilir; facet mevcut
  FilterOptions ucunun genişlemesidir; filtre sorgusu vitrinde koşar.
- Yalnız kapalı-listeli (Option-tipli) özellikler vardır; serbest metin/HTML/link türleri,
  özellik grupları, renk kareleri, atama-başına filtre bayrağı, yönetim ekranı ve agent/MCP
  yüzeyi genişletmesi kapsam dışıdır.
- Tohum içeriği: elektronik/ev-aletleri feed'ine uyan 3-4 özellik (ör. Renk, Materyal, Garanti
  Süresi, Enerji Sınıfı) + değerleri; mock feed rev'lerine örnek attributes eklenir.
- 042 davranış logu değişmez; filtreli liste mevcut impression kaydını basmaya devam eder.
- Saf domain mantığı (birleştirme kuralı, kapalı-liste guard'ı, atama invariant'ları) anayasa
  İlke VI gereği test-first yazılır.
