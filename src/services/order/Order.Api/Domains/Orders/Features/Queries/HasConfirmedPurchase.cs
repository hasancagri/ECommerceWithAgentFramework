namespace Order.Api.Domains.Orders.Features.Queries;

// 044 R1/R2: Reviews'in satin-alma kaniti sorusu. REST ucu YOK — sahibi gRPC
// (OrderPurchaseGrpcService); aggregate REST-penceresi istisnasi (akis sahibi kanal tek giris).
public static class HasConfirmedPurchase
{
    public record HasConfirmedPurchaseQuery(Guid UserId, Guid ProductId);

    public class HasConfirmedPurchaseResponse
    {
        public bool HasPurchase { get; set; }
    }

    public class HasConfirmedPurchaseQueryHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<HasConfirmedPurchaseResponse>> Handle(
            HasConfirmedPurchaseQuery query, CancellationToken ct)
        {
            // Confirmed siparis + kalemde urun (adet/siparis sayisi onemsiz — R1).
            var hasPurchase = await session.Query<Order>()
                .Where(x => x.BuyerId == query.UserId
                            && x.Status == OrderStatus.Confirmed
                            && x.OrderItems.Any(i => i.ProductId == query.ProductId))
                .AnyAsync(ct);

            return FeatureObjectResultModel<HasConfirmedPurchaseResponse>.Ok(
                new HasConfirmedPurchaseResponse { HasPurchase = hasPurchase });
        }
    }
}
