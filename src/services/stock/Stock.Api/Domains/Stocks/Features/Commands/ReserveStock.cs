namespace Stock.Api.Domains.Stocks.Features.Commands;

// 012: sepete ekleme/artirma -> kullanicinin bu urun icin rezervasyonunu mutlak adede getirir.
// Idempotent (ayna model). gRPC StockReservationGrpcService bunu IMessageBus ile cagirir; endpoint yok.
public static class ReserveStock
{
    // 017: ExpiresAt (sepet capasi, mutlak) opsiyonel; null => sabit TTL (geriye uyumlu, Order yolu).
    public record ReserveStockCommand(Guid ProductId, Guid UserId, int Quantity, DateTimeOffset? ExpiresAt = null);

    public class ReserveStockResponse
    {
        public Guid ProductId { get; set; }
        public int Available { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }

    [Transactional]
    public class ReserveStockCommandHandler
    {
        public async Task<FeatureObjectResultModel<ReserveStockResponse>> Handle(
            ReserveStockCommand cmd,
            IDocumentSession session,
            IOptions<ReservationOptions> options,
            CancellationToken ct)
        {
            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId, ct);

            if (stock is null)
                return FeatureObjectResultModel<ReserveStockResponse>.Error(
                    new MessageItem { Code = StockResourceConstants.RECORD_NOT_FOUND });

            var now = DateTimeOffset.UtcNow;
            var result = stock.SetReservedQuantity(cmd.UserId, cmd.Quantity, options.Value.Ttl, now, cmd.ExpiresAt);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<ReserveStockResponse>.Error(result.Messages);

            session.Store(stock);

            var reservation = stock.Reservations.FirstOrDefault(r => r.UserId == cmd.UserId);
            return FeatureObjectResultModel<ReserveStockResponse>.Ok(new ReserveStockResponse
            {
                ProductId = stock.ProductId,
                Available = stock.AvailableAt(now),
                ExpiresAt = reservation?.ExpiresAt
            });
        }
    }
}