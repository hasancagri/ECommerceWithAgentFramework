# Data Model: Supplier Gateway + State'siz Ingestion

## Kanonik integration event — `SupplierProductSnapshotReceived`

`Shared.IntegrationEvents` içinde `record`. Kaydın tedarikçideki güncel hali; diff değil, snapshot.

| Alan | Tip | Not |
|------|-----|-----|
| SupplierCode | string | Tedarikçi kimliği alan olarak (tip değil); ilk sürümde sabit "supplier" |
| ExternalId | string | Tedarikçi harici kimliği; Catalog'da SKU olarak kullanılır (005/R11) |
| Name | string | Ürün adı |
| Description | string | Açıklama |
| Brand | string | Marka; doğrulama Catalog'un işi (BrandType enum, 005 kararı) |
| Price | decimal | Satış fiyatı |
| StockQuantity | int | Mutlak stok (full snapshot) |
| DiscountPercent | decimal? | 0-100; null = indirim yok → remove yolu |

Feed'deki `DiscountCode` kontrata alınmaz (research R2 — yazım yolu yalnız rate kullanır).

## Gateway dokümanı — `FeedSnapshot` (supplierGatewayDb / supplierGatewayManagement)

Harici kimlik başına en son YAYINLANAN kanonik içerik. Durum alanı yoktur; "işlendi" bilgisi tutmaz.

| Alan | Tip | Not |
|------|-----|-----|
| Id | string | = ExternalId (tek tedarikçi; 005 emsali) |
| Content | (kanonik kayıt) | Son yayınlanan snapshot; record değer eşitliğiyle kıyaslanır |
| PublishedAtUtc | DateTime | Son yayın anı (teşhis amaçlı) |

**Davranış** (kapı kararı modelin metodunda, executor/servis yalnız orkestre eder):

- `IsUnchanged(incoming)` → Content == incoming (record `==`; hash yok).
- `Absorb(incoming)` → Content'i günceller, PublishedAtUtc damgalar. Publish BAŞARISINDAN sonra çağrılır.

**Durum geçişi**: yok → var (ilk yayın); var → üstüne yaz (değişiklik). Silme yolu yok (kapsam dışı).

## Agent iş dosyası — `RecordJob` (kalıcı değil, workflow içi)

Mesaj başına workflow'dan akar; hiçbir alanı persist edilmez.

| Alan | Tip | Not |
|------|-----|-----|
| Message | SupplierProductSnapshotReceived | Gelen kanonik mesaj |
| ProductId | Guid? | CatalogWrite doldurur (upsert cevabından) |
| CatalogAction | string? | "created" / "updated"; StockWrite'ın atlama kararı |
| Failure | string? | Dolarsa handler exception'a çevirir → retry/DLQ |

Eski alanlar (Staging, Skipped, AssumedNew, WriteStock, SetDiscount, RemoveDiscount) silinir;
karar artık mesaj içeriği + senkron tool cevabından verilir.

## Silinen modeller

- `StagingRecord`, `StagingStatus`, `WriteDecision` — değişiklik kapısı Gateway'e taşındı.
- `IngestionRun`, `SupplierRunResult`, `FetchStatus` — run kavramı öldü; görünürlük kuyruk + DLQ.
- `FeedRecord` (agent kopyası) — tel modeli artık kanonik event; Gateway kendi adapter DTO'sunu kullanır.

## Değişen domain davranışı (Discount)

`Features/Agent/RemoveProductDiscount`: komut sonucu NotFound ise `Ok` döner (idempotent agent yüzü).
Domain command'ı, REST DELETE ucu (404) ve `DiscountChangedEvent` yayını değişmez.