# Contract: Integration Events (Shared.IntegrationEvents)

Tüm event'ler fat (self-contained) — Storefront event'i aldıktan sonra kaynağa geri
dönüp ek veri çekmez (research.md madde 3). Her event `OccurredAtUtc` taşır;
Storefront handler'ları bunu stale-event guard için kullanır (data-model.md upsert
kuralları). Hepsi `ProductId` ile anahtarlı.

## ProductChangedEvent — YENİ

Yayıncı: `Catalog.Api` (`CreateProduct`, `UpdateProduct`, `DeleteProduct`
handler'ları). Hem oluşturma hem güncelleme hem soft-delete için AYNI event kullanılır.

```csharp
public record ProductChangedEvent(
    Guid ProductId,
    string Name,
    string? ImageUrl,
    bool IsDeleted,
    DateTime OccurredAtUtc);
```

Tüketici: `Storefront.Api` → `CatalogInfo` upsert.

Not: Mevcut `ProductCreatedEvent` (Stock'un tükettiği) DOKUNULMAZ — farklı amaç
(Stock'un başlangıç stok kaydı açması). `ProductChangedEvent` ayrı, yeni bir event'tir.

## StockChangedEvent — YENİ

Yayıncı: `Stock.Api` (`IncreaseStock`, `DecreaseStock` handler'ları VE mevcut
`ProductCreatedHandler`'ın başlangıç stok kaydı açtığı an).

```csharp
public record StockChangedEvent(
    Guid ProductId,
    bool IsInStock,      // Quantity > 0
    DateTime OccurredAtUtc);
```

Tüketici: `Storefront.Api` → `StockInfo` upsert.

## DiscountChangedEvent — YENİ

Yayıncı: `Discount.Api` (`SetProductDiscount`, `RemoveProductDiscount` handler'ları —
research.md madde 7, ürün-bazlı modele dönüşüm).

```csharp
public record DiscountChangedEvent(
    Guid ProductId,
    decimal? Rate,        // null = indirim kaldırıldı
    DateTime OccurredAtUtc);
```

Tüketici: `Storefront.Api` → `DiscountInfo` upsert.

## RabbitMqConstants eklemeleri

```csharp
public static class ProductChanged
{
    public const string Exchange = "product.changed";
    public static class Queues { public const string Storefront = "storefront.product-changed"; }
}

public static class StockChanged
{
    public const string Exchange = "stock.changed";
    public static class Queues { public const string Storefront = "storefront.stock-changed"; }
}

public static class DiscountChanged
{
    public const string Exchange = "discount.changed";
    public static class Queues { public const string Storefront = "storefront.discount-changed"; }
}
```

Order/Payment kapsam dışı olduğu için `OrderCreated` exchange'ine dokunulmaz, Payment'a
RabbitMQ kablolaması eklenmez.