# Research: Catalog Domain Extract — Eşleme Kararları

Tüm kararlar 2026-08-19 brainstorming oturumunda kullanıcıyla kapatıldı; NEEDS CLARIFICATION kalmadı.
Referans implementasyon repo içinde: `src/otherProjects/CustomNopCommerce` (Catalog-Core).

## K1 — Sadakat yönü

- **Decision**: Staging modeli esas alınır; ana repoya özgü işlev (Brand, ImageUrl, NormalizedName, embedding akışı,
  event kontratı) üstüne bindirilir.
- **Rationale**: Kullanıcı kararı: "Yapıyı olduğu gibi değiştirebilirsin". Strangler-fig yönü staging→ana repo.
- **Alternatives considered**: Yalnız Gtin ekleyip modeli ince bırakmak — reddedildi (kullanıcı tam extract istedi).

## K2 — Fiyat: Money VO, dış kontrat decimal

- **Decision**: `Product.Price` `Money` VO olur (Amount + Currency, TRY varsayılan). `ProductChangedEvent.Price`
  decimal KALIR; event yayınlanırken `Price.Amount` yazılır.
- **Rationale**: Storefront/tüketiciler kırılmaz; BC-içi zenginlik dış kontrata sızmak zorunda değil (İlke I).
- **Alternatives considered**: Event'e Money koymak — reddedildi (tüm tüketiciler + read-model değişirdi, parity bozulur).

## K3 — Gtin bu feature'da boş

- **Decision**: `Gtin` alanı modele girer, feed kontratı değişmediği için boş kalır. 041 dolduracak.
- **Rationale**: Extract davranış eşitliği; feed kontrat değişikliği 041'in işi (buy-box eşleşme anahtarı).
- **Alternatives considered**: Feed'e şimdi barkod eklemek — reddedildi (iki feature'ın kapsamı karışır).

## K4 — Kategori: çoklu atama modelde, tek atama akışta

- **Decision**: `ProductCategoryAssignment` listesi gelir. Ingestion/komutlar TEK kategori atar (ilk atama = primary).
  Event'e `Categories[0]` yazılır. "Kategorisiz ürün olmaz" kuralı handler'da sürer (en az 1 atama).
- **Rationale**: Model nopCommerce'e sadık; dış davranış bugünle aynı.
- **Alternatives considered**: Tek CategoryId alanını korumak — reddedildi (staging şeklinden sapma).

## K5 — Category aggregate hizası

- **Decision**: Staging alanları gelir (Description, ParentCategoryId, DisplayOrder, Published, ShowOnHomepage, Seo).
  Ana repodaki `NormalizedName` + computed unique index AYNEN korunur (ingestion dedup buna dayanır).
- **Rationale**: İkisi çelişmez; NormalizedName teklik anahtarı 016 kararıdır, ingestion parity için şart.
- **Alternatives considered**: NormalizedName'i atmak — reddedildi (CategoryWrite dedup kırılır).

## K6 — Brand ana repoda kalır

- **Decision**: Staging'de Brand yok; ana repo `Brand` aggregate'i ve `Product.BrandId` aynen kalır.
- **Rationale**: Ingestion BrandWrite + event kontratı (BrandId/Brand) buna dayanır; nopCommerce Manufacturer
  staging'e hiç alınmamış.
- **Alternatives considered**: Manufacturer modeli getirmek — reddedildi (yeni özellik olur, parity ihlali).

## K7 — ImageUrl korunur

- **Decision**: `ImageUrl` zengin Product'a alan olarak eklenir (staging'de yok ama ana repo işlevi).
- **Rationale**: Görsel servis topolojisi (File.Api + gateway-relative yol) çalışan özellik; düşürülemez.
- **Alternatives considered**: Görseli ayrı modele taşımak — reddedildi (kapsam dışı).

## K8 — Published semantiği

- **Decision**: Ingestion/upsert yazımı ürünü publish eder (bugünkü "yazılan ürün vitrindedir" davranışı).
  Unpublish yolu modelde var, akış bağlanmaz (041 buy-box "kazanan yok → kapat" için kullanacak).
- **Rationale**: Davranış eşitliği; 041'e hazır zemin.
- **Alternatives considered**: Published'ı hiç almamak — reddedildi (041 ön koşulu).

## K9 — ProductTag gelir, besleyen akış yok

- **Decision**: `ProductTag` aggregate + `Product.TagIds` gelir; feed etiket vermez, koleksiyon boş yaşar.
  Endpoint/MCP yüzeyi eklenmez (yeni özellik olurdu); yalnız domain + testleri.
- **Rationale**: Kullanıcı tam yapı istedi; dış yüzey eklememek parity'yi korur.
- **Alternatives considered**: Tag'ı tamamen atlamak — düşünüldü; "olduğu gibi değiştir" kararına aykırı bulundu.

## K10 — Migration yok, DB reset

- **Decision**: catalogDb (ve storefront read-model) sıfırlanır; feed replay katalog yeniden kurar.
- **Rationale**: Mevcut proje pratiği (016'da da DB sıfırdan); ürünler yalnız feed'den gelir, veri kaybı yok.
- **Alternatives considered**: Marten belge dönüşümü/patch — reddedildi (öğrenme projesinde gereksiz tören).

## K11 — Grouped ürün alanları pasif

- **Decision**: `ProductType` + `ParentGroupedProductId` modele girer; feed hep Simple üretir, Grouped akışı yok.
- **Rationale**: Staging şekline sadakat; davranış eklemeden alan taşımak parity'yi bozmaz.
- **Alternatives considered**: ProductType'ı atlamak — reddedildi (staging şeklinden sapma).

## K12 — IngestionAgent LLM yazıcıları kalır

- **Decision**: 015 LLM yazıcı zinciri bu feature'da aynen kalır; yalnız CatalogWrite'ın çağırdığı upsert yeni modele
  uyarlanır (tool imzası sabit). LLM'siz deterministik yol 041'in kararıdır.
- **Rationale**: Extract tek eksende değişiklik yapar; ingestion mimarisini değiştirmek 041 kapsamı.
- **Alternatives considered**: Şimdi deterministik yola geçmek — reddedildi (iki büyük değişiklik tek PR'da riskli).
