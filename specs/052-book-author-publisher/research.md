# Research: Kitap Yazar + Yayınevi Modeli

Faz 0 — belirsizlik çözümü + mevcut kod keşfi. Kaynak: Catalog + Storefront kod haritası (2026-08-28).

## Mevcut durum (keşif özeti)

- **Product** (`Catalog.Api/Domains/Products/Product.cs`): tek `Guid BrandId` (satır 51), tek mutasyon `SetBrand` (187–192). Koleksiyon yok.
- **Brand** (`Domains/Brands/Brand.cs`): `Name`+`NormalizedName`, `Create` fabrikası, rename YOK (016), unique computed index (Program.cs:26).
- **Import** (`Seeding/ImportBook.cs`): `GetOrCreateBrandAsync` (86–96) normalize+ara+kur; `SetBrand(brand.Id)` (57); yayınlanınca `ProductChangedEvent` + `ProductAdded` (66–74).
- **Event** (`Shared/IntegrationEvents.cs` 15–28): `ProductChangedEvent` fat — `Guid BrandId, string Brand` taşır (tüketici lookup yapmaz).
- **Storefront read-model** (`StorefrontView.cs` 21–22): `Guid? BrandId`+`string? Brand`; `ApplyCatalog` yazar; facet in-memory `GroupBy(BrandId)` (`GetStorefrontFilterOptions.cs` 52–57); liste filtresi `BrandId`/`Brand` (`GetStorefrontProductList.cs` 36–39).
- **Varyant gruplama** (`GetStorefrontProductList.cs` 116–136): `FamilyKey = FamilyCode ?? ProductId`. **Brand'e DEĞMİYOR.**

## Kararlar

### D1 — Çok-yazar Product'ta liste + Id referansı
- **Karar:** `Product.BrandId (Guid)` → `_authorIds (List<Guid>)` private + `IReadOnlyList<Guid> AuthorIds`; `SetBrand` → `SetAuthors(IEnumerable<Guid>)`. Yazarlar Id ile referanslanır (İlke II: aggregate'ler arası Id ile).
- **Gerekçe:** Veride 66 çok-katkıcı kayıt; dürüst model kullanıcı kararı. Marten `List<Guid>`'i jsonb dizi olarak tutar; filtre `Where(x => x.AuthorIds.Contains(id))` jsonb'ye çevrilir.
- **Alternatif (red):** Ayrı `ProductAuthor` join aggregate — İlke II'ye aykırı (anemik), gereksiz; VO/Id-liste yeterli.

### D2 — Author = Brand'in rename'i
- **Karar:** `Domains/Brands/` → `Domains/Authors/`; `Brand` tip → `Author` (aynı `Name`+`NormalizedName`+`Create`+`JasperFxIgnore`). Unique index korunur. `BRAND_ALREADY_EXISTS` → `AUTHOR_ALREADY_EXISTS`. REST endpoint'leri (CreateBrand/GetBrands) Author'a rename (JIT: tüketen yoksa sadeleştirilebilir, iskelet kalır).
- **Gerekçe:** Ubiquitous dil (kitapyurdu künyesi = "Yazar"). Brand zaten yazarı tutuyordu; isim yanlıştı.

### D3 — Publisher = yeni aggregate (Author kalıbı)
- **Karar:** `Domains/Publishers/Publisher.cs` — `Name`+`NormalizedName`+`Create`, immutable, unique computed index. `Product`'a tek `Guid PublisherId` + `SetPublisher(Guid)`.
- **Gerekçe:** Kitabın tek yayınevi olur (kitapyurdu birebir). Kendi kimliği/invariant'ı (tekil ad) var → İlke II'ye göre meşru aggregate, VO değil.

### D4 — Yayınevi uydurma, build-zamanı, ISBN-kararlı
- **Karar:** `shape_books.py` her kayda deterministik yayınevi atar: `md5(isbn) % 4` → 4 havuz. `books.json`'a `publisher` alanı yazılır (commit'li, görünür). Import `GetOrCreatePublisherAsync` ile Publisher aggregate'i get-or-create eder (Brand kalıbı).
- **Gerekçe:** Veride yayınevi yok (1427'de 2). Build-zamanı bakma = kararlı (aynı ISBN hep aynı), tekrar-üretilebilir, JSON'da denetlenebilir. Python `hash()` salt'lı → **kullanma**; `hashlib.md5` deterministik.
- **Havuz:** `Can Yayınları`, `İletişim Yayınları`, `İş Bankası Kültür Yayınları`, `Yapı Kredi Yayınları`.
- **Alternatif (red):** Import-zamanı atama — çalışma-anı, JSON'da görünmez, aynı sonuç ama daha az denetlenebilir.

### D5 — Contributors = KAPSAM DIŞI (YAGNI, kullanıcı kararı 2026-08-29)
- **Karar:** Yazar-dışı katkıcı (illüstratör/editör/anlatıcı/derleyen) **tutulmaz**. Contributor VO/enum/event-alanı/read-model-alanı hiç girmez. Ayrıştırmada yazardan ayrılıp **atılır**.
- **Gerekçe:** Yalnız 16/1427 kitapta (%1) var (Illustrator 11, Compiler 5, Editor 2, Narrator 1). 5 katmana (shape/VO/event/read-model/DTO) tesisat %1 için erken. Katalog gerçek veriyle büyüyünce eklenir.
- **Alternatif (red):** Contributor VO + event alanı — kontrat/read-model kirliliği, %1 getiri.

### D6 — shape_books.py rol-etiketi temizliği
- **Karar:** `brand` string'i ayrıştır: virgül/`;` ile böl; her token'dan trailing `(Rol)` çıkar; `& N more`/`by ` at.
  - `(Author)` etiketliler → `authors`. Hiç etiket yoksa tüm string tek yazar.
  - `(Illustrator|Narrator|Editor|Compiler|...)` yazar-dışı roller → **atılır** (saklanmaz).
  - Hiç `(Author)` yok (yalnız yazar-dışı rol) → yazar `["Unknown"]`.
- **Çıktı json:** `authors: string[]`, `publisher: string`; eski `brand` + contributor kalkar.
- **Doğrulama:** script yeniden çalıştırılır, sayımlar (yazarlı %, publisher 4-dağılım, yazar-dışı sızma yok) quickstart'ta kontrol.

### D7 — Event kontrat evrimi (kırıcı, tek-tüketici, koordine)
- **Karar:** `ProductChangedEvent`'ten `BrandId`+`Brand` çıkar; ekle: `List<AuthorRef> Authors` (Id+Name çift), `Guid PublisherId`+`string Publisher`. Contributor alanı YOK. `ProductAdded` değişmez.
- **Gerekçe:** Konvansiyon "additive default" bağımsız-deploy güvenliği içindir; burada tek tüketici (Storefront), aynı PR'da güncelleniyor, DB sıfırdan seed → ölü `Brand` alanı taşımak dürüstsüz. Temiz kesim.
- **Paired records:** `AuthorRef(Guid Id, string Name)` — facet id+name eşlemesi paralel-liste kırılganlığı olmadan taşınsın.

### D8 — Storefront read-model + facet
- **Karar:** `StorefrontView`: `BrandId`/`Brand` → `List<AuthorRef> Authors`; ekle `PublisherId`+`Publisher`. Contributor alanı YOK. `ApplyCatalog` güncellenir.
- **Author facet** (çok-değerli): `rows.SelectMany(r => r.Authors).GroupBy(a => a.Id)` → id+ad; Brand'in GroupBy'ı çok-değerliye flatten olur.
- **Publisher facet:** `GroupBy(PublisherId)` — bugünkü Brand facet birebir.
- **Liste filtresi:** yazar = `Where(x => x.Authors.Any(a => a.Id == id))` (jsonb); yayınevi = `PublisherId==id`.
- **Detay DTO:** authors listesi + publisher gösterilir.
- **Cache:** facet "filters" key invalidation aynen (Catalog event'i tetikler).

### D9 — Varyant gruplama: değişiklik YOK
- **Karar:** `FamilyKey`/`PickRepresentative`/`GroupToRepresentatives` brand'e değmiyor → **dokunulmaz**. Spec FR-012 kod-değişikliği gerektirmez; "yazardan bağımsız" zaten sağlanmış.
- **Gerekçe:** Keşif kanıtı (`GetStorefrontProductList.cs` 116–136). Spec'teki varsayım (tek-FK gruplamayı etkiler) yanlıştı; düzeltilir.

### D10 — Catalog MCP/SearchProducts yazar filtresi
- **Karar:** `SearchProducts.cs` (49) brand→BrandId filtresi → yazar adı çöz→id, `AuthorIds.Contains(id)`. `search_products` MCP param `brand`→`author` parity.

## Test kapsamı (İlke VI — test-first)

Saf domain birimleri (xUnit+Shouldly, implementasyondan ÖNCE):
- `Author.Create`, `Publisher.Create` (boş ad hatası, normalize).
- `Product.SetAuthors` (çoklu, boş liste reddi, dedup davranışı), `Product.SetPublisher`.

Kapsam dışı (test-sonra/canlı): handler, ImportBook, event wiring, Storefront query, `shape_books.py` (build-tool; çıktı-sayım doğrulaması, xUnit değil).

## Açık risk

- **Marten `List<Guid>.Contains` / `.Any()` jsonb çevirisi** — standart destekli; yine de canlı doğrulamada yazar filtresi ilk kontrol edilecek (043'te `MatchesSql` benzeri sürprizler yaşandı).
- **Aynı yazar farklı yazım** ("Emily Brontë"/"Emily Bronte") normalize edebildiği kadar birleşir; aksan farkı ayrı kalır (bilinçli sınır, spec edge-case).