# Feature Specification: Catalog Domain Extract (Zengin nopCommerce Modeli)

**Feature Branch**: `040-catalog-domain-extract`

**Created**: 2026-08-19

**Status**: Draft

**Input**: User description: "Catalog.Api'nin ince domain modeli, CustomNopCommerce staging monolith'indeki zengin
nopCommerce-uyarlanmış Catalog-Core modeliyle değiştirilir. Davranış eşitliği hedefi: yeni özellik yok. Feature 2
(multi-supplier Procurement + buy-box) için Gtin ön koşuldur. CustomNopCommerce süreç sonunda silinecek staging alanıdır."

## Bağlam

- Strangler-fig yönü: staging monolith'te (`src/otherProjects/CustomNopCommerce`) olgunlaşan domain ana repoya taşınır.
- Bu feature YALNIZ Catalog BC'nin domain modelini değiştirir; davranış eşitliği korunur (yeni kullanıcı özelliği yok).
- Arkasından gelecek 041 (multi-supplier + Procurement + buy-box) bu extract'in `Gtin` alanına dayanır.
- CustomNopCommerce koduna dokunulmaz; o referans kaynaktır, süreç sonunda silinecektir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mevcut alışveriş akışı aynen çalışır (Priority: P1)

Müşteri ve sistem, extract'ten habersizdir: feed'den gelen ürün vitrine düşer, aranır, sepete girer, siparişe döner.

**Why this priority**: Davranış eşitliği bu feature'ın tanımıdır; bozulursa extract başarısızdır.

**Independent Test**: Aspire ayağa kalkar; feed ingest sonrası vitrin, arama, sepet ve checkout uçtan uca koşulur.

**Acceptance Scenarios**:

1. **Given** boş katalog, **When** tedarikçi feed'i işlenir, **Then** ürünler bugünkü akışla vitrine düşer.
2. **Given** vitrindeki ürün, **When** müşteri sepete ekleyip checkout yapar, **Then** sipariş bugünkü gibi tamamlanır.
3. **Given** vitrindeki ürünler, **When** müşteri arama yapar (hybrid search), **Then** sonuçlar bugünkü gibi döner.

---

### User Story 2 - Catalog domaini zengin modele geçer (Priority: P2)

Geliştirici, Catalog BC'de nopCommerce-uyarlanmış zengin modeli bulur: değer nesneli fiyat, barkod alanı, ürün türü,
davranış metotları, çoklu kategori ataması ve etiket desteği.

**Why this priority**: Extract'in kendisi; 041'in (buy-box) ön koşulu olan `Gtin` bu modelle gelir.

**Independent Test**: Product/Category/ProductTag davranış metotları saf domain birim testleriyle doğrulanır.

**Acceptance Scenarios**:

1. **Given** yeni ürün, **When** geçersiz girdiyle davranış metodu çağrılır (örn. boş ad), **Then** hata Result'ı döner.
2. **Given** ürün, **When** aynı kategoriye ikinci atama denenir, **Then** invariant reddeder (hata Result'ı).
3. **Given** ürün, **When** etiket iki kez eklenir, **Then** işlem idempotent kalır (tek kayıt).

---

### User Story 3 - Agent ve ingestion yüzeyi parity korur (Priority: P3)

Chat kullanıcısı ürün sorabilir; ingestion zinciri (Brand/Category/Catalog/Stock yazıcıları) feed'i kataloğa yazar.

**Why this priority**: MCP/agent yüzeyi ve LLM yazıcılar modele dokunur; kırılırsa ingestion ve chat durur.

**Independent Test**: Feed mesajı IngestionAgent'tan geçer; chat'ten ürün sorgusu MCP tool'la yanıt bulur.

**Acceptance Scenarios**:

1. **Given** yeni feed kaydı, **When** IngestionAgent işler, **Then** marka+kategori+ürün+stok bugünkü gibi yazılır.
2. **Given** katalogdaki ürün, **When** chat'ten ürün aranır, **Then** agent MCP tool'u ile sonuç döner.

---

### Edge Cases

- Feed'de barkod yok: `Gtin` boş kalır; sistem boş Gtin'le tam çalışır (041 dolduracak).
- Eski veri: migration YOK — katalog DB'si sıfırlanır, feed replay ile yeniden kurulur (ürünler yalnız feed'den).
- Dışa giden ürün event'i sayısal fiyat taşımaya devam eder; tüketiciler (Storefront) değişiklik hissetmez.
- Çoklu kategori modeli gelir ama ingestion tek kategori atar; dış kontratta tek kategori görünmeye devam eder.
- Ürün türü: feed her zaman Simple üretir; Grouped alanları boş kalır, akışları bu feature'da doğrulanmaz.
- Ölçü (Dimensions) ve SEO alanları feed'den dolmaz; boş varsayılanla yaşar, hiçbir akışı bloklamaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Catalog ürün modeli zengin şekle geçer: kısa+tam açıklama, Sku, Gtin, MPN, ürün türü, yayın bayrakları.
- **FR-002**: Fiyat, tutar+para birimi taşıyan değer nesnesi olur; dış kontratlarda bugünkü sayısal fiyat korunur.
- **FR-003**: Tüm ürün mutasyonları davranış metotlarından geçer ve Result döner; invariant aggregate içinde korunur.
- **FR-004**: Ürün-kategori ilişkisi çoklu atamaya döner (featured/displayOrder ile); aynı kategoriye çift atama reddedilir.
- **FR-005**: Etiket (ProductTag) modeli Catalog BC'ye gelir; ürüne etiket ekleme/çıkarma idempotenttir.
- **FR-006**: Ana repoya özgü yetenekler korunur: Brand ilişkisi, ImageUrl, hybrid search embedding akışı.
- **FR-007**: Vitrin yayın kararı `Published` bayrağına taşınır; bugünkü "kategorisiz ürün olmaz" kuralı sürer.
- **FR-008**: Catalog MCP tool'ları ve Agent slice'ları aynı sözleşmeyle çalışmaya devam eder (chat kırılmaz).
- **FR-009**: IngestionAgent yazıcıları (Brand/Category/Catalog/Stock) yeni modele yazar; akış davranışı değişmez.
- **FR-010**: Ürün event'i tüketicileri (Storefront) bugünkü bilgi kümesini almaya devam eder; read-model bozulmaz.
- **FR-011**: Veri migration yapılmaz; katalog feed replay ile sıfırdan kurulur ve bu yol dokümante edilir.
- **FR-012**: CustomNopCommerce'e bu feature'da hiçbir değişiklik yapılmaz (yalnız referans).

### Key Entities

- **Product**: Zengin aggregate — kimlik (Sku/Gtin/MPN), sunum, tür, fiyat VO, ölçü, SEO, kategori/etiket atamaları.
- **Category**: Mevcut aggregate; staging'deki şekle hizalanır (hiyerarşi/sıra alanları staging neyse o).
- **Brand**: Ana repoda kalır (staging'de yok); ürün Id ile referans verir (016 düzeni sürer).
- **ProductTag**: Yeni aggregate; ad taşır, ürünler Id ile referans verir.
- **Money**: Fiyat değer nesnesi (tutar + para birimi).
- **ProductDimensions / SeoMetadata / ProductCategoryAssignment**: Ürüne bağlı değer nesneleri.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Çözüm derlenir; mevcut ve yeni tüm testler yeşildir.
- **SC-002**: Canlı akış: feed ingest sonrası ürünler vitrinde görünür; sayı/davranış extract öncesiyle aynıdır.
- **SC-003**: Canlı akış: sepete ekle + checkout uçtan uca extract öncesiyle aynı sonuçla tamamlanır.
- **SC-004**: Canlı akış: chat'ten ürün sorgusu yanıt döndürür.
- **SC-005**: Zengin ürün davranışları (ad, fiyat, kategori, etiket, yayın) birim testlidir ve test-first yazılmıştır.

## Assumptions

- Katalog verisi migration'sız sıfırdan kurulur; feed tek kaynak (mevcut "ürünler yalnız feed'den" kuralı).
- Feed kontratı bu feature'da DEĞİŞMEZ; Gtin feed'e 041 ile girer, o zamana dek boş kalır.
- Storefront read-model şeması ve WebApp ekranları değişmez; tek kategori görünümü sürer (primary = ilk atama).
- Basket/Order/Stock/Payment BC'lerine dokunulmaz (BC izolasyonu; kendi modelleri etkilenmez).
- Staging'deki `Published` vitrin kararıyla bugünkü "her ürün vitrinde" davranışı eşitlenir: ingestion yazımı publish eder.
- Fiyat para birimi tek (TRY); Money VO çoklu para birimini modellese de akışlar tek para birimiyle çalışır.
