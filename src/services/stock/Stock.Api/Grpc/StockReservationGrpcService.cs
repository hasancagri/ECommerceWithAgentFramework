namespace Stock.Api.Grpc;

// 012: Stock rezervasyon gRPC sunucusu. Ince sarici — is mantigi aggregate'te, burada yalniz
// Wolverine command'ini IMessageBus ile cagirir (MCP tool deseninin gRPC muadili). Yetki endpoint
// seviyesinde (.RequireAuthorization(StockReserve)); userId cagri govdesinde tasinir.
public class StockReservationGrpcService(IMessageBus bus)
    : Shared.Grpc.Stock.StockReservation.StockReservationBase
{
    public override async Task<ReservationReply> SetReservedQuantity(
        SetReservedQuantityRequest request, ServerCallContext context)
    {
        // 017: bos/parse edilemeyen expires_at yok sayilir (sabit TTL'e duser) — savunmaci, yeni hata kodu yok.
        DateTimeOffset? expiresAt =
            DateTimeOffset.TryParse(request.ExpiresAt, out var parsed) ? parsed : null;

        var result = await bus.InvokeAsync<FeatureObjectResultModel<ReserveStock.ReserveStockResponse>>(
            new ReserveStock.ReserveStockCommand(
                Guid.Parse(request.ProductId), Guid.Parse(request.UserId), request.Quantity, expiresAt),
            context.CancellationToken);

        if (!result.IsSuccess)
            return Fail(FirstCode(result.Messages));

        var data = result.Data!;
        return new ReservationReply
        {
            Success = true,
            Status = ReservationStatus.Ok,
            Available = data.Available,
            ExpiresAt = data.ExpiresAt?.ToString("O") ?? string.Empty,
            MessageCode = string.Empty
        };
    }

    public override async Task<ReservationReply> Release(ReleaseRequest request, ServerCallContext context)
    {
        var result = await bus.InvokeAsync<FeatureObjectResultModel<ReleaseStock.ReleaseStockResponse>>(
            new ReleaseStock.ReleaseStockCommand(Guid.Parse(request.ProductId), Guid.Parse(request.UserId)),
            context.CancellationToken);

        if (!result.IsSuccess)
            return Fail(FirstCode(result.Messages));

        return new ReservationReply
        {
            Success = true,
            Status = ReservationStatus.Ok,
            Available = result.Data!.Available,
            MessageCode = string.Empty
        };
    }

    public override async Task<ReservationReply> Commit(CommitRequest request, ServerCallContext context)
    {
        var result = await bus.InvokeAsync<FeatureObjectResultModel<CommitStock.CommitStockResponse>>(
            new CommitStock.CommitStockCommand(
                Guid.Parse(request.ProductId), Guid.Parse(request.UserId), request.Quantity),
            context.CancellationToken);

        if (!result.IsSuccess)
            return Fail(FirstCode(result.Messages));

        return new ReservationReply
        {
            Success = true,
            Status = ReservationStatus.Ok,
            Available = result.Data!.Available,
            MessageCode = string.Empty
        };
    }

    private static ReservationReply Fail(string code) => new()
    {
        Success = false,
        Status = MapStatus(code),
        Available = 0,
        MessageCode = code
    };

    private static string FirstCode(List<MessageItem>? messages) =>
        messages?.FirstOrDefault()?.Code ?? string.Empty;

    private static ReservationStatus MapStatus(string code)
    {
        if (code == StockResourceConstants.STOCK_INSUFFICIENT) return ReservationStatus.InsufficientStock;
        if (code == StockResourceConstants.STOCK_NO_ACTIVE_RESERVATION) return ReservationStatus.NoActiveReservation;
        if (code == StockResourceConstants.RECORD_NOT_FOUND) return ReservationStatus.ProductNotFound;
        return ReservationStatus.Unspecified;
    }
}