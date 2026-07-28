namespace Basket.Api.Domains.Baskets;

// 017: sepet capasinin suresi (FR-013). Basket politikasi — Stock'un Reservations:Ttl'inden ayri.
public sealed class BasketReservationOptions
{
    public const string SectionName = "Basket";

    // Ilk basarili eklemede kurulan capa suresi. Varsayilan 5 dk.
    public TimeSpan ReservationDuration { get; set; } = TimeSpan.FromMinutes(5);
}