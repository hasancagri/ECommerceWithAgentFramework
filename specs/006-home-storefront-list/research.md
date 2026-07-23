# Research: 006-home-storefront-list

Phase 0 kararları. Spec'te NEEDS CLARIFICATION yoktu; kararlar mevcut kod desenlerinden türetildi.

## K1 — Event genişletme: yerinde mi, yeni event mi?

- **Decision**: Mevcut `ProductChangedEvent` yerinde genişletilir; v2/yeni event üretilmez.
- **Rationale**: Tek repo; yayıncı (Catalog) ve tüketici (Storefront) birlikte derlenip birlikte deploy olur.
  Eski (fiyatsız) satırlar spec gereği dev reset + ingestion yeniden koşusuyla dolar; kontrat versiyonlama gereksiz tören olur.
- **Alternatives considered**: `ProductChangedEventV2` (çift kontrat bakımı, dev ortamda değersiz); ayrı `ProductPriceChangedEvent` (üç yayıncı iki event yayınlar, kazanım yok).

## K2 — Event'te marka tipi: string mi BrandType mı?

- **Decision**: Event ve StorefrontView'da marka `string` taşınır (`BrandType.ToString()` adı).
- **Rationale**: Kontrat primitive kalır; Storefront, Catalog'un enum semantiğine bağlanmaz (BC izolasyonu dostu). İhtiyaç yalnız görüntüleme.
- **Alternatives considered**: `BrandType` (Shared.Enums zaten paylaşımlı ama tüketiciyi enum evrimine kilitler); int değer (görüntüleme için tekrar çeviri gerekir).

## K3 — Liste sonuç tipi: FeatureListResultModel mı?

- **Decision**: `FeatureObjectResultModel<List<T>>` kullanılır (Catalog `GetAllProducts` emsali).
- **Rationale**: `FeatureListResultModel` boş listeyi otomatik NotFound yapar; US1-AS2 boş vitrinde 200 + boş liste ister ("ürün bulunamadı" UI durumu).
- **Alternatives considered**: `FeatureListResultModel<T>` (boş vitrin hata sayfasına düşerdi — spec ihlali).

## K4 — Storefront liste query'sinde cache var mı?

- **Decision**: `[Cached]` eklenmez; liste her istekte Marten'dan okunur.
- **Rationale**: SC-002 5 sn tazelik ister. `[InvalidatesCache]` command yoluna weave edilir; Storefront'u besleyen Wolverine event handler'ları
  bu yoldan geçmez, invalidasyon tetiklenmezdi. Read model zaten önbellek görevi görür (adr-cache-vs-readmodel sınırı).
- **Alternatives considered**: kısa TTL L1-only cache (5 sn tazelik garantisini bulanıklaştırır, kazanım ölçülmedi).

## K5 — WebApp erişimi: gateway mi doğrudan servis mi?

- **Decision**: Diğer tüm Refit istemcileri gibi service discovery ile doğrudan `http://storefront-api`; kayıt aynı handler zinciriyle yapılır.
- **Rationale**: WebApp'teki 6 mevcut istemcinin deseni birebir bu; anonim ziyaretçide handler'lar pass-through çalışıyor (ana sayfa bugün de anonim).
- **Alternatives considered**: gateway üzerinden (WebApp'te emsali yok; gateway `ClientCredential` politikası anonim okumayı gereksiz sertleştirir).

## K6 — Liste sıralaması

- **Decision**: `Name` artan sıralanır (null Name'ler zaten filtrelenir).
- **Rationale**: `StorefrontView` denetim alanı taşımaz (BaseModel değil); deterministik ve kullanıcıya anlamlı tek alan Name.
- **Alternatives considered**: sırasız (sayfa her yenilemede karışabilir); ProductId (kullanıcıya anlamsız).

## K7 — "Dolu satır" filtresi

- **Decision**: Liste filtresi `!IsDeleted && Name != null && Price != null`.
- **Rationale**: FR-005/FR-006; Price null'luğu "Catalog fat verisi henüz gelmedi"nin işareti (eski satırlar dahil). FR-007 gereği IsAvailableForSale filtrelenmez.
- **Alternatives considered**: yalnız `Name != null` (fiyatsız eski satırlar kartı bozar — spec edge case'i ihlal).

## K8 — Stok/indirim rozet türetimi

- **Decision**: `IsInStock` sunucuda türetilir (`StockQuantity > 0`, null ise null) — `GetProductStorefrontView` emsali. Rozet çizimi WebApp'te.
- **Rationale**: Türetim tek yerde kalır; UI yalnız null/0/pozitif üçlüsünü çizer (FR-009).
- **Alternatives considered**: türetimi UI'da yapmak (iki tüketici olursa kural kopyalanır).