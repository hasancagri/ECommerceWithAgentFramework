namespace Stock.Api.Domains.Stocks.Entities;

// 012-stock-reservation: ProductStock aggregate'ine ait gomulu entity (bagimsiz yasamaz).
// Bir kullanicinin bir urun icin ayirdigi adet + bitis zamani. Kimlik = (ProductStock, UserId).
// Sade entity (base almaz); mutasyon yalniz aggregate metotlarindan gecer.
public class StockReservation
{
    private StockReservation() { }

    public StockReservation(Guid userId, int quantity, DateTimeOffset expiresAt)
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
}