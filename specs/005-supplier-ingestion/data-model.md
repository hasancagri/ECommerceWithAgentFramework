# Data Model: Tedarikçi Entegrasyonu (005)

İki yeni depo: `supplierDb`/`supplierManagement` (simülatör) ve `ingestionDb`/`ingestionManagement` (staging).
Domain servislerinin modelleri (Product, ProductStock, Discount) değişmez; yalnız Stock'a bir davranış eklenir.
Tüm tedarikçiler tek doküman tipinde toplanır (`SupplierCode` ayırt eder); gerekçe: SC-006, şema tedarikçiyle değişmez.

## Supplier.Api — SupplierProduct (düz doküman, aggregate değil)

Kaynağın kendisini simüle eden tipli, temiz kayıt (kanonik veri seti). Marten/jsonb ile saklanır.

| Alan | Tip | Not |
|------|-----|-----|
| Id | Guid | Marten dokümanı |
| SupplierCode | string | `acme` / `nordic` / `tekno` |
| ExternalId | string | Tedarikçi içinde benzersiz (ör. `ACM-1001`) |
| Name | string | |
| Description | string | |
| Brand | string | Temiz marka adı (ör. `Apple`) |
| Price | decimal | Nokta ondalık |
| StockQuantity | int | |
| DiscountCode | string? | Opsiyonel kampanya etiketi |
| DiscountPercent | decimal? | Opsiyonel, 0–100 |

- `Datasets/{acme,nordic,tekno}.json`'dan açılışta seed edilir (doluysa atlanır).
- Bozuk kayıt simülasyonu veri setinde eksik alanla yapılır (ör. boş Name, boş Brand).

## IngestionAgent — FeedRecord (ara model, tek başına kalıcı değil)

Adapter çıktısı; tüm tedarikçiler bu tek modele çevrilir (ACL). Kanonik JSON'u hash'lenir.

| Alan | Tip | Not |
|------|-----|-----|
| SupplierCode | string | |
| ExternalId | string | |
| Name | string | Zorunlu; boşsa kayıt Failed |
| Description | string | |
| RawBrand | string | Feed'den gelen marka metni |
| Price | decimal | ≤ 0 ise Failed |
| StockQuantity | int | < 0 ise Failed |
| Barcode | string? | Bugün doldurulmaz (FR-015, kapı açık) |
| DiscountCode | string? | Domain'e yazılmaz |
| DiscountPercent | decimal? | Varsa 0 < x ≤ 100; aksi Failed |

## IngestionAgent — StagingRecord (Marten dokümanı)

Aynı kayıtta iki katman yan yana: ham tel verisi (`RawPayload`) + standardize içerik (`Normalized`).

| Alan | Tip | Not |
|------|-----|-----|
| Id | string | Deterministik: `{SupplierCode}:{ExternalId}` |
| SupplierCode | string | |
| ExternalId | string | |
| RawPayload | string | Telden geldiği ham hali: CSV satırı / XML parçası / JSON objesi (FR-010, SC-005) |
| ContentHash | string | Ara modelin kanonik JSON'unun SHA-256'sı |
| Normalized | FeedRecord | Son denenen normalize içerik; fark tespitinin kıyas tabanı |
| MappedBrand | string? | `BrandType` adı; eşlenemezse null + Failed |
| CatalogProductId | Guid? | İlk create sonrası dolar; update bu Id ile yapılır |
| Status | enum | `Pending` / `Processing` / `Completed` / `Failed` |
| ErrorReason | string? | Failed ise zorunlu |
| ProcessedAtUtc | DateTime? | Son işlenme zamanı |

- Düz enum kullanılır (Enumeration değil): domain modeli değil, teknik iz dokümanı (bkz. plan Complexity Tracking).

### Durum geçişleri

```text
(yeni kayıt)              → Pending → Processing → Completed | Failed
Completed + hash değişti  → Processing → Completed | Failed   (güncelleme)
Failed (sonraki run'da)   → Processing → Completed | Failed   (FR-021 yeniden deneme)
Completed + hash aynı     → dokunulmaz (Skipped sayacı artar, FR-012)
```

### Fark tespiti (deterministik, agent'a sorulmaz)

- `hash aynı` → Skipped; hiçbir agent çağrılmaz.
- `CatalogProductId == null` → Create yolu: CatalogAgent `create_product` (InitialStock dahil; StockAgent gerekmez).
  Yeni kayıtta DiscountPercent doluysa → DiscountAgent `set_product_discount`.
- `hash farklı` → Update yolu: CatalogAgent `update_product`; ek adımlar eski `Normalized` ile kıyasla belirlenir:
  - `StockQuantity` değişti → StockAgent `set_stock`.
  - `DiscountPercent` değişti (dolu) → DiscountAgent `set_product_discount`.
  - `DiscountPercent` doluydu, boş geldi → DiscountAgent `remove_product_discount` (FR-026).

## IngestionAgent — IngestionRun (Marten dokümanı)

| Alan | Tip | Not |
|------|-----|-----|
| Id | Guid | |
| StartedAtUtc | DateTime | |
| FinishedAtUtc | DateTime? | |
| Status | enum | `Running` / `Completed` / `Failed` |
| Suppliers | List\<SupplierRunResult\> | Tedarikçi kırılımı (FR-022) |

**SupplierRunResult**: `SupplierCode`, `FetchStatus` (`Fetched`/`Unreachable`/`Empty`),
`New`, `Updated`, `Skipped`, `Failed` (int sayaçlar).

- Feed erişilemezse: o tedarikçi `Unreachable`, run devam eder; `Failed` run yalnız beklenmeyen çökmede.
- Aynı feed'de mükerrer ExternalId: ilk kayıt esas; sonrakiler Failed (`DUPLICATE_EXTERNAL_ID`).

## Stock.Api — mevcut aggregate'e eklenen davranış

- `ProductStock.SetQuantity(int quantity)`: mutlak adet atar; `quantity < 0` → Result hata (resource sabitiyle).
- Yeni slice: `Features/Commands/SetStock.cs` (`[Transactional]`, `[RequiredScope(StockWrite)]`).

## Değişmeyenler

- `Product`, `Discount` aggregate'leri ve şemaları aynen kalır; yalnız MCP yazma tool'ları (ince sarmalayıcı) eklenir.
- `Shared.IntegrationEvents`'e yeni event eklenmez; stok oluşumu mevcut `ProductCreatedEvent` ile akar.