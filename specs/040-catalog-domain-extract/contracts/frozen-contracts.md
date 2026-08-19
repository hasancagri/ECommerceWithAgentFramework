# Contracts: SABİT Kalan Dış Kontratlar

Bu feature dış kontrat DEĞİŞTİRMEZ. Aşağıdakiler donmuş sözleşmedir; implementasyon bunlara uymak zorundadır.
Değişen her şey Catalog BC içidir.

## 1. Integration Event — ProductChangedEvent (Shared.IntegrationEvents)

```csharp
public record ProductChangedEvent(
    Guid ProductId, string Name, string Description, decimal Price,
    Guid BrandId, string Brand, Guid CategoryId, string Category,
    string? ImageUrl, bool IsDeleted);
```

- `Price` decimal KALIR; kaynak `Product.Price.Amount` (K2).
- `Description` = FullDescription.
- `CategoryId/Category` = primary atama (Categories[0]) + kategori adı (K4).
- `IsDeleted` hep false (016 kuralı sürer).
- Yayın noktaları aynı kalır: CreateProduct, UpdateProduct, UpsertProduct (agent slice).

## 2. Integration Event — SupplierProductSnapshotReceived

Değişmez (Gtin 041'de eklenecek). Bu feature feed kontratına dokunmaz.

## 3. Catalog MCP tool imzaları

- `ProductMcpTools`, `CategoryMcpTools`, `BrandMcpTools`: tool adları, parametreleri ve `[Description]`'ları
  AYNEN kalır. İç eşleme (yeni modele okuma/yazma) serbest.
- IngestionAgent'ın çağırdığı upsert tool imzaları sabit — LLM prompt'ları değişmeden çalışmalı.

## 4. REST uçları (Catalog.Api)

- Route'lar, HTTP metotları ve response şekilleri (Response sınıfları) dışa görünür alan adlarıyla korunur.
- Fiyat response'larda sayısal (decimal) görünmeye devam eder.

## 5. Storefront read-model

- `StorefrontEventHandlers` ve `StorefrontView` satır şeması değişmez; event kontratı korunduğu için
  Storefront koduna dokunulmaz. Embedding/hybrid search akışı (019) etkilenmez.
