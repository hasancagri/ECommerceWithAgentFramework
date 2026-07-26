namespace Stock.Api.Domains.Stocks;

// 012-stock-reservation: rezervasyon TTL'i ve sweep periyodu config'den okunur (FR-010).
// appsettings "Reservations" bolumu; test icin kisaltilabilir.
public sealed class ReservationOptions
{
    public const string SectionName = "Reservations";

    // Sabit TTL varsayilani 15 dk (FR-010). ExpiresAt yenilenmez (FR-010a).
    public TimeSpan Ttl { get; set; } = TimeSpan.FromMinutes(15);

    // Hangfire sweep cron (US4). Varsayilan dakikalik.
    public string SweepCron { get; set; } = "* * * * *";
}