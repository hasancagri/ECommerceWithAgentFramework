namespace Shared;

public static class IntegrationEvents
{
    // 028: OrderCreatedEvent kaldirildi — sepet temizligi CheckoutSaga'nin gRPC adimina tasindi.

    // 003-storefront-read-model: writer-publishes, fat event'ler (Storefront pull-back yapmaz).
    // 006-home-storefront-list: Description/Price/Brand eklendi.
    // 016-category-brand: kimlik + ad birlikte taşınır (R7); Id opak değerdir, tüketici lookup yapmaz.
    // Kategori zorunludur (kullanıcı kararı 2026-07-27): kategorisiz ürün domain'de yoktur.
    public record ProductChangedEvent(
        Guid ProductId,
        string Name,
        string Description,
        decimal Price,
        Guid BrandId,
        string Brand,
        Guid CategoryId,
        string Category,
        string? ImageUrl,
        bool IsDeleted);
    public record StockChangedEvent(Guid ProductId, int Quantity);

    // 012-stock-reservation: TTL dolunca Stock yayinlar; Basket ilgili sepet satirini siler.
    public record ReservationExpired(Guid ProductId, Guid UserId);
}