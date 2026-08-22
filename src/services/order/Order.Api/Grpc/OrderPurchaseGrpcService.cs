namespace Order.Api.Grpc;

// 044: satin-alma kaniti gRPC sunucusu. Ince sarici — is mantigi yok, Wolverine query'sini
// IMessageBus ile cagirir (012 StockReservationGrpcService deseni). Yetki endpoint seviyesinde
// (reviews.write, R4); sub==user_id guard'i burada: kimse baskasinin kanitini soramaz.
public class OrderPurchaseGrpcService(IMessageBus bus)
    : Shared.Grpc.Order.OrderPurchase.OrderPurchaseBase
{
    public override async Task<Shared.Grpc.Order.HasConfirmedPurchaseReply> HasConfirmedPurchase(
        Shared.Grpc.Order.HasConfirmedPurchaseRequest request, ServerCallContext context)
    {
        var sub = context.GetHttpContext().User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var callerId)
            || !Guid.TryParse(request.UserId, out var userId)
            || callerId != userId)
            throw new RpcException(new Status(StatusCode.PermissionDenied, "sub/user_id mismatch"));

        var result = await bus.InvokeAsync<FeatureObjectResultModel<
                Domains.Orders.Features.Queries.HasConfirmedPurchase.HasConfirmedPurchaseResponse>>(
            new Domains.Orders.Features.Queries.HasConfirmedPurchase.HasConfirmedPurchaseQuery(
                userId, Guid.Parse(request.ProductId)),
            context.CancellationToken);

        return new Shared.Grpc.Order.HasConfirmedPurchaseReply
        {
            HasPurchase = result.IsSuccess && result.Data!.HasPurchase
        };
    }
}
