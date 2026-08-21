# Data Model: Ürün Özellikleri ve Facet Filtre (043)

BC-başına model; paylaşılan tek şey Shared.IntegrationEvents kontratı (AD sözleşmesi).

## Shared.IntegrationEvents

- **ProductSpec** (record): `Attribute` (string), `Option` (string) — kanonik AD çifti.
- `CanonicalProductUpserted.Specs: List<ProductSpec>` (yeni, default boş — additive).
- `ProductChangedEvent.Specs: List<ProductSpec>` (yeni, default boş — additive).

## Supplier.Api

- **SupplierFeedRow** += `Attributes: Dictionary<string, string>?` — ham tedarikçi anahtar/değer
  (ör. `"COLOR": "BLACK"`); yokluk eski davranış. Dataset rev JSON'larına elle örnek eklenir.

## Procurement.Api

### Seed (statik, CanonicalSpecs.cs; ProcurementSeedHostedService uygular)

- **SpecDefinition**: `Name`, `Filterable` (MVP'de hep true), `DisplayOrder`, `Options: string[]`.
  Seed içeriği: Renk(Siyah,Beyaz,Gri,Kırmızı), Materyal(Çelik,Plastik,Cam,Ahşap),
  Garanti Süresi(1 Yıl,2 Yıl,3 Yıl), Enerji Sınıfı(A,B,C).
- **SpecValueMapping**: `RawKey`, `RawValue`, `Attribute`, `Option` — tedarikçi-başına liste
  (supplier-a Türkçe anahtarlar: "Renk"/"Siyah"; supplier-b İngilizce: "COLOR"/"BLACK").

### PoolProduct (mevcut aggregate genişler)

- **SupplierListing** += `RawAttributes: Dictionary<string, string>` (boş sözlük default).
  Hash-diff'e dahil (attributes değişimi de yeni rev sayılır).
- **CanonicalContent** += `Specs: IReadOnlyList<(Attribute, Option)>` — merge + enrich sonucu.
  `Status` HESABINA GİRMEZ (FR-005: spec eksikliği Pending yapmaz).
- **EnrichmentResult** += `Specs` (AI seçimleri; kapalı-liste guard'ından geçmiş hali saklanır).
- **Merge kuralı** (`RebuildCanonical`): attribute-başına — aktif listing'ler Priority sırasında
  gezilir, eşlenmiş ilk dolu değer kazanır; eşlenemeyen ham anahtar yok sayılır; enrich overlay
  yalnız merge'in boş bıraktığı attribute'ları doldurur.
- **Guard** (`ApplyEnrichment`): registry'de olmayan attribute/option çifti reddedilir (ResultDomain
  Error; satır spec'siz ilerler).

## Catalog.Api

### SpecificationAttribute (yeni aggregate, Domains/SpecificationAttributes/)

| Alan | Tip | Not |
|------|-----|-----|
| Name | string | görünen ad |
| NormalizedName | string | unique index (Category/Brand emsali) |
| Filterable | bool | MVP seed'inde true |
| DisplayOrder | int | detay tablosu + facet sırası |
| Options | child list | `SpecificationAttributeOption` entity: Id, Name, DisplayOrder |

- Davranışlar: `Create`, `Rename`, `AddOption` (boş/mükerrer ad guard), `SetFilterable`.
- Seed: `CatalogSpecSeedHostedService` — get-or-create (NormalizedName), Procurement seed'iyle AYNI
  adlar (sözleşme=AD, bilinçli tekrar).
- REST penceresi: List + Create + AddOption (ProductTag emsali).

### Product (mevcut aggregate genişler)

- **ProductSpecificationAssignment** (record VO, ProductValueObjects.cs): `AttributeId: Guid`,
  `OptionId: Guid` — Id-referans (İlke II).
- `_specifications` private list → `IReadOnlyList<ProductSpecificationAssignment> Specifications`.
- `SetSpecifications(assignments)`: tam-değiştirme; aynı AttributeId'nin tekrarında hata
  (bir attribute = tek değer). Handler: ProcurementEventHandlers (ad→Id çözümü handler'da).
- Event yayını: handler, Specs adlarını registry'den Id'ye çevirir; ProductChangedEvent'e adlar
  geri yazılır (Id'ler event'e ÇIKMAZ).

## Storefront.Api

### StorefrontView (mevcut satır genişler)

- `Specs: List<SpecPair>` — `SpecPair(Attribute, Option)` (detay + facet üretimi).
- `SpecKeys: string[]` — `"Attribute|Option"` düz anahtarları (sorgu; ApplyCatalog türetir).
- `ApplyCatalog(...)` imzasına specs parametresi eklenir (event'ten).

### Sorgular

- **GetStorefrontFilterOptions** yanıtı += `Specifications: [{ Name, Options: [{ Name, Count }] }]`
  — yalnız yayındaki satırlardan; Count = o çifti taşıyan satır sayısı (SC-006).
- **GetStorefrontProductList** += `Specs: string[]` parametresi ("Attribute|Option"); attribute
  grubu içi VEYA (jsonb `?|` MatchesSql), gruplar arası VE; kategori/marka/sayfalama ile birleşir.
- **GetStorefrontProduct** (tekil) yanıtı += `Specs` listesi (detay tablosu).

## WebApp

- `StorefrontProductViewModel` += `Specs` (detay için); `FilterOptionsViewModel` += spec facet'leri.
- `GetProductsAsync(..., specs)` — query-string `spec=Attribute|Option` (çoklu anahtar).

## Doğrulama kuralları (test-first hedefleri)

1. Merge: iki tedarikçi farklı attribute → birleşir; aynı attribute → düşük Priority dolu kazanır;
   sıra-bağımsızlık (listing sırası karışık verilir).
2. Guard: liste-dışı attribute/option reddi; kısmi geçerli enrich çıktısında geçerliler uygulanır.
3. Atama: mükerrer AttributeId hatası; tam-değiştirme eski atamaları düşürür.
4. ApplyFilters: grup içi OR, gruplar arası AND; boş specs = filtre yok; kategori/marka ile birleşim.
