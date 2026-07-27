# Research: Kategori ve Marka (016)

Kesinleşen kararlar ve gerekçeleri. Keşif kaynakları: Catalog/Supplier/Storefront kod taraması (2026-07-27).

## R1 — Category/Brand modeli: aggregate (VO reddedildi)

- **Decision**: Catalog BC'de 3 aggregate root: `Product`, `Category`, `Brand`. Category/Brand kimlikli kayıttır.
- **Rationale**: Teklik invariant'ı (NormalizedName unique) kayıt-üstü bir kuraldır; VO bunu taşıyamaz. Kimlik,
  ürünün `BrandId`/`CategoryId` ile referans vermesini ve adın tek yerde yaşamasını sağlar.
- **Alternatives considered**: Value object (record, üründe gömülü ad) — REDDEDİLDİ: teklik/normalizasyon kuralı
  dağılır, aynı ad her üründe kopyalanır. Ayrı bir "Taxonomy" servisi — aşırı; kavram Catalog'un parçası.
- **Anayasa**: v1.3.0 amendment (İlke II) birden çok zengin aggregate'e izin verir; anemiklik yasağı sürer.
  Category/Brand davranışı: fabrika normalizasyonu + teklik anahtarı üretimi (Create), immutable ad.

## R2 — Doğum ve yaşam döngüsü: yalnız feed'den get-or-create, ad immutable

- **Decision**: Category/Brand YALNIZ ingestion (upsert) sırasında get-or-create ile doğar. Rename/CRUD/yönetim yok.
- **Rationale**: Kullanıcı kararı ("işlendikten sonra isim değişmez"); feed veri girişinin tek kapısıdır (007/014).
- **Alternatives considered**: Admin CRUD ekranı — REDDEDİLDİ (kapsam ve ihtiyaç yok). Rename desteği — REDDEDİLDİ.

## R3 — Normalizasyon ve teklik anahtarı

- **Decision**: `NormalizedName` = trim + iç boşlukları tek boşluğa toplama + `ToUpperInvariant`. Karşılaştırma ve
  teklik bu anahtar üzerinden; görünen `Name` ilk gelen yazımla saklanır.
- **Rationale**: Feed yazım farkları ("Elektronik" vs " elektronik ") tek kayda bağlanmalı (FR-009, edge case).
- **Not**: Invariant kültür kullanılır; TR 'i/İ' farkı iki tarafta da aynı fonksiyonla üretildiği sürece tutarlıdır.
- **Alternatives considered**: Slug/kültüre-özel lower — gerek yok; anahtar dışa sızmaz, yalnız eşleşme içindir.

## R4 — Teklik zorlaması: Marten computed unique index + yakın yarışta yeniden okuma

- **Decision**: `opts.Schema.For<Brand>().UniqueIndex(UniqueIndexType.Computed, x => x.NormalizedName)` (Category aynı).
  Get-or-create (`UpsertBrand`/`UpsertCategory` agent slice'ları, R10): NormalizedName ile sorgula → yoksa
  Create+Store; unique ihlalinde bir kez yeniden okuyup mevcut Id kullan.
- **Rationale**: Kuyruk tek tüketicili, yarış nadir; DB indeksi son güvence. Repo'da örnek: Stock `Index(...)` deseni.
- **Alternatives considered**: Uygulama içi kilit/advisory lock — gereksiz karmaşıklık.

## R5 — BrandType enum silinir; mevcut veri migrasyonu

- **Decision**: `Shared/Enums/BrandType.cs` ve 14 kullanım noktası silinir/değişir. Marten'daki eski `Product` dokümanları
  `Brand`'i int (Newtonsoft enum default) tutar; property silinince Marten eski üyeyi yok sayar (tolere edildi).
  Catalog açılışında bir kerelik idempotent migrasyon koşar: `BrandId`'siz dokümanlar için ham JSON'dan eski int okunur,
  sabit legacy haritayla (1=Apple … 10=Xiaomi) ada çevrilir, Brand get-or-create edilir, `BrandId` patch'lenir.
- **Rationale**: SC-004 (%100 marka korunumu). Feed backfill (R6) feed kaynaklı ürünleri zaten günceller; elle
  oluşturulmuş ürünleri yalnız migrasyon yakalar. Storefront satırları ad taşıdığından yeniden yayın gerekmez.
- **Alternatives considered**: Yalnız feed backfill'e güvenmek — REDDEDİLDİ (elle oluşturulan ürün feed'de yok).

## R6 — Feed'e kategori alanı; doğal backfill

- **Decision**: `Supplier.Api/Datasets/products.json` 500 kayda genişletilir; TÜM kayıtlar kategorili olur
  (kategorisiz kayıt yok — kullanıcı kararı); mevcut 200 kayıt korunur, 300 yeni kayıt (SUP-1201…SUP-1500) üretilir;
  `SupplierProduct`/`SupplierFeedRecord`/`SupplierProductSnapshotReceived` + `string? Category`.
- **Rationale**: Gateway diff'i record value-equality'dir; yeni alan her snapshot'ı "değişti" yapar → 500 kayıt
  yayınlanır (200 güncel + 300 yeni) → katalog kendiliğinden dolar (doğal backfill, elle adım yok).
  Genişletme kullanıcı isteği (2026-07-27): daha zengin kategori/marka dağılımı ve daha gerçekçi liste.
- **Not**: Kategorisi boş kayıt yine reddedilmez (`CategoryId=null` savunma toleransı, FR-010); ancak dataset
  bilinçli olarak tam kategorili tutulur.

## R7 — Sınırda zenginleştirme: fat event Id + AD birlikte taşır

- **Decision**: `ProductChangedEvent` marka/kategori için kimlik + adı birlikte taşır: `BrandId`, `Brand`,
  `CategoryId?`, `Category?`. Yayın anında Catalog adları yükleyip event'e koyar (kullanıcı kararı 2026-07-27).
- **Rationale**: Ad görüntü içindir; Id stabil referanstır (ör. 017 publish onayı, olası gelecek senaryolar).
  Id'ler opak değer olarak taşınır (ProductId gibi); tüketici Catalog'a geri dönüp lookup YAPMAZ.
- **Alternatives considered**: Yalnız ad (önceki karar) — ad immutable olsa da ileriye dönük esneklik için genişletildi.
  Yalnız Id + tüketicide lookup — REDDEDİLDİ (çapraz-BC bağımlılık doğurur).

## R8 — Filtre ve facet: Storefront üzerinden, kimlik VEYA adla

- **Decision**: `GetStorefrontProductList` opsiyonel `categoryId`/`brandId` (Guid) ve `category`/`brand` (ad)
  paramları alır; Id verilmişse Id, yoksa ad eşleşmesi uygulanır. Yeni facet query satılabilir satırlardan
  `Distinct` kimlik+ad çiftleri döner; boş/null kategori facet'te listelenmez.
- **Rationale**: Liste deneyimi Storefront'tan servis edilir (003/006/011); facet aynı veriden gelmeli ki
  "ürünü olmayan seçenek görünmez" (US1-3) kendiliğinden sağlansın. Cache yok duruşu (K4) korunur.
- **Alternatives considered**: Facet'i Catalog'dan çekmek — REDDEDİLDİ (boş kategoriler görünürdü; ek BC çağrısı).

## R9 — WebApp form ve asistan daraltması

- **Decision**: WebApp ürün formlarında BrandType dropdown'ı yerine Catalog'dan beslenen marka listesi (`BrandId`);
  kategori opsiyonel dropdown. Asistan için `search_products` MCP tool'una opsiyonel `category`/`brand` paramı eklenir;
  Catalog `SearchProducts` slice'ı normalize adla Id çözüp filtreler.
- **Rationale**: Marka feed'den doğar; form yalnız var olanı seçtirir. Asistan mevcut Catalog MCP hattını kullanır.
- **Alternatives considered**: Formdan serbest metin marka — REDDEDİLDİ (doğum yalnız feed'den, FR-004).

## R10 — Ingestion workflow'una 2 yeni executor: BrandWrite + CategoryWrite

- **Decision**: MAF zinciri 5 yazıcıya çıkar: `BrandWrite → CategoryWrite → CatalogWrite → StockWrite →
  DiscountWrite → Finish` (kullanıcı kararı 2026-07-27). Brand/Category adımları yeni `upsert_brand` /
  `upsert_category` MCP tool'larını çağırır (get-or-create, Id döner); `upsert_product` artık ad değil
  `brandId` (Guid) + `categoryId` (Guid?) alır. Herhangi bir adım başarısızsa short-circuit → Finish
  (015 deseni; retry/DLQ devralır).
- **Rationale**: 015'in tipli zincir deseniyle tutarlı — her yazım kendi adımında, Id'ler zincirde akar;
  Catalog upsert handler'ında ad çözümü tekrarlanmaz (çift iş yok). Marka zorunlu olduğundan kesme doğal.
- **Not**: Kategori adı boş gelirse CategoryWrite executor'ı LLM/tool çağırmadan deterministik olarak
  `categoryId=null` geçirir (FR-010 savunma toleransı).
- **Alternatives considered**: Get-or-create'i `UpsertProduct` handler'ına gömmek (bu planın ilk hali) —
  kullanıcı kararıyla ayrı executor'lara taşındı. Kategori hatasında null ile devam — REDDEDİLDİ (kesme tercih edildi).