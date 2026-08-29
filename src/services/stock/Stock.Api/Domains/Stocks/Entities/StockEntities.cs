namespace Stock.Api.Domains.Stocks.Entities;

// 012-stock-reservation: ProductStock aggregate'ine ait gomulu entity (bagimsiz yasamaz).
// Bir kullanicinin bir urun icin ayirdigi adet + bitis zamani. Kimlik = (ProductStock, UserId).
// Sade entity (base almaz); mutasyon yalniz aggregate metotlarindan gecer.
public class ReservationEntry
{
    private ReservationEntry() { }

    public ReservationEntry(Guid userId, int quantity, DateTimeOffset expiresAt)
    {
        UserId = userId;
        Quantity = quantity;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }
    public int Quantity { get; private set; }

    // Sabit TTL (FR-010a): ExpiresAt yalniz ilk olusumda atanir; SetQuantity onu YENILEMEZ.
    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsActiveAt(DateTimeOffset now) => ExpiresAt > now;

    // Adedi degistirir; ExpiresAt'e DOKUNMAZ (yenileme yok).
    public void SetQuantity(int quantity) => Quantity = quantity;

    // 017: yalniz aggregate acik mutlak bitis (sepet capasi) aldiginda cagirir; sabit-TTL yolunda kullanilmaz.
    public void SetExpiresAt(DateTimeOffset expiresAt) => ExpiresAt = expiresAt;
}

// 041: barkod ↔ ProductId eşleme dokümanı (aggregate DEĞİL — read-model satırı gibi düz eşleme).
// ProductAdded handler'ı yazar (idempotent upsert); kanonik güncelleme tüketimi bu eşlemeden çözer.
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