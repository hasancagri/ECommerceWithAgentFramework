# Contract: Integration Events (Shared.IntegrationEvents)

> 2026-07-20 revizyonu (as-built): event'lerden `OccurredAtUtc` KALDIRILDI (timestamp
> stale-guard kalktı, sıralama listener `.Sequential()` ile); `StockChangedEvent`
> `bool IsInStock` yerine `int Quantity` taşır.

Tüm event'ler fat (self-contained) — Storefront event'i aldıktan sonra kaynağa geri
dönüp ek veri çekmez (research.md madde 3). Hepsi `ProductId` ile anahtarlı. Sıralama
Storefront tarafında listener `.Sequential()` (kaynak-içi FIFO) ile sağlanır — event
kendi içinde bir timestamp taşımaz.

## ProductChangedEvent — YENİ

Yayıncı: `Catalog.Api` (`CreateProduct`, `UpdateProduct`, `DeleteProduct`
handler'ları). Hem oluşturma hem güncelleme hem soft-delete için AYNI event kullanılır.

```csharp
public record ProductChangedEvent(
    Guid ProductId,
    string Name,
    string? ImageUrl,
    bool IsDeleted);
```

Tüketici: `Storefront.Api` → `StorefrontView` (Catalog alanları) upsert.

Not: Mevcut `ProductCreatedEvent` (Stock'un tükettiği) DOKUNULMAZ — farklı amaç
(Stock'un başlangıç stok kaydı açması). `ProductChangedEvent` ayrı, yeni bir event'tir.

## StockChangedEvent — YENİ

Yayıncı: `Stock.Api` (`IncreaseStock`, `DecreaseStock` handler'ları VE mevcut
`ProductCreatedHandler`'ın başlangıç stok kaydı açtığı an).

```csharp
public record StockChangedEvent(
    Guid ProductId,
    int Quantity);       // gerçek adet; in-stock Storefront'ta Quantity > 0'dan türetilir
```

Tüketici: `Storefront.Api` → `StorefrontView.StockQuantity` upsert.

## DiscountChangedEvent — YENİ

Yayıncı: `Discount.Api` (`SetProductDiscount`, `RemoveProductDiscount` handler'ları —
research.md madde 7, ürün-bazlı modele dönüşüm).

```csharp
public record DiscountChangedEvent(
    Guid ProductId,
    decimal? Rate);       // null = indirim kaldırıldı
```

Tüketici: `Storefront.Api` → `StorefrontView.DiscountRate` upsert.

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