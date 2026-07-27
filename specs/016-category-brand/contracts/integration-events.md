# Kontrat: Integration Event'leri (016 revizyonu)

Dosya: `src/others/Shared/IntegrationEvents.cs`. Taşıma: RabbitMQ fanout (adlar `RabbitMqConstants`, değişmez).

## SupplierProductSnapshotReceived (yeni şekil)

```csharp
public record SupplierProductSnapshotReceived(
    string SupplierCode,
    string ExternalId,
    string Name,
    string Description,
    string Brand,
    string? Category,        // YENİ — wire'da nullable (dış veri); boş/eksik kayıt ingestion'da kesilir (FR-010)
    decimal Price,
    int StockQuantity,
    decimal? DiscountPercent);
```

- Üretici: Supplier.Gateway (outbox). Tüketici: IngestionAgent (`ingestion.supplier-product-snapshot`).
- Not: alan eklenmesi record-equality diff'ini tetikler; ilk pull 500 kaydın tümünü yayınlar (bilinçli backfill).

## ProductChangedEvent (yeni şekil)

```csharp
public record ProductChangedEvent(
    Guid ProductId,
    string Name,
    string Description,
    decimal Price,
    Guid BrandId,            // YENİ — stabil referans (opak değer; tüketici lookup yapmaz)
    string Brand,            // Brand.Name (artık enum ToString değil)
    Guid CategoryId,         // YENİ — zorunlu (kategorisiz ürün yok; kullanıcı kararı 2026-07-27)
    string Category,         // YENİ — Category.Name (zorunlu)
    string? ImageUrl,
    bool IsDeleted);
```

- Üretici: Catalog (Create/Update/Delete/Upsert). Tüketici: Storefront (`ApplyCatalog`).
- Kural: kimlik + görünen ad birlikte taşınır (research R7); ad görüntü, Id referans içindir.