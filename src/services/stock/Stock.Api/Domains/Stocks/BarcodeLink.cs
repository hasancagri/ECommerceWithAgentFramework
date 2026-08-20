namespace Stock.Api.Domains.Stocks;

// 041: barkod ↔ ProductId eşleme dokümanı (aggregate DEĞİL — read-model satırı gibi düz eşleme).
// ProductLinked handler'ı yazar (idempotent upsert); BuyBoxChanged tüketimi bu eşlemeden çözer.
public class BarcodeLink
{
    public string Id { get; private set; } = default!; // barkod
    public Guid ProductId { get; private set; }

    private BarcodeLink()
    {
    }

    public static BarcodeLink Create(string barcode, Guid productId)
        => new() { Id = barcode, ProductId = productId };
}