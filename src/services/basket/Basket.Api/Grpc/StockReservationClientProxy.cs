
namespace Basket.Api.Grpc;

public sealed class ReservationResult
{
    public bool Success { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string Code { get; init; } = string.Empty;
    public int Available { get; init; }
}

// 012: Basket -> Stock rezervasyon gRPC cagrilarini sarar. gRPC hatasinda FAIL-CLOSED
// (Stock erisilemezse ekleme reddedilir; oversell yasak, FR-018). Release best-effort.
public sealed class StockReservationClientProxy(StockReservation.StockReservationClient client)
{
    public const string ReserveUnavailable = "STOCK_RESERVE_UNAVAILABLE";

    // 012 robustluk: deadline'siz gRPC, hung Stock'ta (Aspire proxy arkasi) cagriyi askida birakir.
    // Deadline -> DeadlineExceeded RpcException -> mevcut catch fail-closed'a duser (hizli reddetme).
    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(5);

    // 017: expiresAt = sepet capasi (mutlak). Her cagrida gecirilir; rezervasyon bu bitisle hizalanir.
    public async Task<ReservationResult> SetReservedQuantityAsync(
        Guid productId, Guid userId, int quantity, DateTimeOffset? expiresAt, CancellationToken ct)
    {
        try
        {
            var reply = await client.SetReservedQuantityAsync(new SetReservedQuantityRequest
            {
                ProductId = productId.ToString(),
                UserId = userId.ToString(),
                Quantity = quantity,
                ExpiresAt = expiresAt?.ToString("O") ?? string.Empty
            }, deadline: DateTime.UtcNow.Add(CallDeadline), cancellationToken: ct);

            return new ReservationResult
            {
                Success = reply.Success,
                ExpiresAt = ParseExpiry(reply.ExpiresAt),
                Code = reply.MessageCode,
                Available = reply.Available
            };
        }
        catch (RpcException)
        {
            return new ReservationResult { Success = false, Code = ReserveUnavailable };
        }
    }

    public async Task ReleaseAsync(Guid productId, Guid userId, CancellationToken ct)
    {
        try
        {
            await client.ReleaseAsync(new ReleaseRequest
            {
                ProductId = productId.ToString(),
                UserId = userId.ToString()
            }, deadline: DateTime.UtcNow.Add(CallDeadline), cancellationToken: ct);
        }
        catch (RpcException)
        {
            // Release best-effort: basarisiz olsa da TTL rezervasyonu er ya da gec serbest birakir.
        }
    }

    private static DateTimeOffset? ParseExpiry(string value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
}