namespace Shared;

public static class IntegrationEvents
{
    // 028: OrderCreatedEvent kaldirildi — sepet temizligi CheckoutSaga'nin gRPC adimina tasindi.

    // 003-storefront-read-model: writer-publishes, fat event'ler (Storefront pull-back yapmaz).
    // 006-home-storefront-list: Description/Price/Brand eklendi.
    // 016-category-brand: kimlik + ad birlikte taşınır (R7); Id opak değerdir, tüketici lookup yapmaz.
    // Kategori zorunludur (kullanıcı kararı 2026-07-27): kategorisiz ürün domain'de yoktur.
    // 043: Specs — kanonik özellik AD çiftleri (Id taşınmaz; sözleşme=AD, taksonomi deseni).
    // Additive + opsiyonel: eski yayıncı/tüketici kırılmaz; null = özellik bilgisi yok (boş sayılır).
    public record ProductSpec(string Attribute, string Option);

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
        bool IsDeleted,
        List<ProductSpec>? Specs = null);
    public record StockChangedEvent(Guid ProductId, int Quantity);

    // 012-stock-reservation: TTL dolunca Stock yayinlar; Basket ilgili sepet satirini siler.
    public record ReservationExpired(Guid ProductId, Guid UserId);

    // 041: Procurement → Catalog. Eksiksiz kanonik ürün (fat — yayın anındaki buy-box Price+Stock dahil,
    // fiyatsız pencere olmaz). Yayın koşulu: kanonik complete VE (içerik hash veya buy-box değişti).
    // Kategori adları kanonik seed ağacından çözülür (NormalizedName); ölçüde 0 = bilinmiyor.
    public record CanonicalProductUpserted(
        string Barcode,
        string Name,
        string Description,
        string Brand,
        string Category,
        string SubCategory,
        string Sku,
        decimal Weight,
        decimal Length,
        decimal Width,
        decimal Height,
        decimal Price,
        int Stock,
        // 043: kanonik özellikler (attribute-başına merge + kapalı-liste enrich sonrası adlar).
        List<ProductSpec>? Specs = null);

    // 041: Procurement → Catalog + Stock. Yalnız BuyBoxDecision değişince yayınlanır (value-eşitlik).
    // SupplierId null = kazanan yok (tüm offer'lar stoksuz/delisted): fiyat son bilinen, stok 0.
    // Bilinmeyen Barcode iki tüketicide de YOK SAYILIR (ilk değerler CanonicalProductUpserted'da taşındı).
    public record BuyBoxChanged(
        string Barcode,
        Guid? SupplierId,
        decimal Price,
        int Stock);

    // 041: Catalog → Stock. Yalnız YENİ ürün oluşunca yayınlanır; Stock BarcodeLink eşlemesini kurar
    // ve OnHand'i InitialStock (CanonicalProductUpserted.Stock) ile mutlak yazar.
    public record ProductLinked(
        string Barcode,
        Guid ProductId,
        int InitialStock);
}