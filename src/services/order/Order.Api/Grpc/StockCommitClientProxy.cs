using Grpc.Core;

namespace Order.Api.Grpc;

public sealed class CommitResult
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
}

// 012 (US2): Order -> Stock Commit gRPC cagrisini sarar. Basarisiz/erisilemez ise siparis
// reddedilir (FR-008; oversell yasak). Odeme zaten alinmis olabilir -> refund kapsam disi.
public sealed class StockCommitClientProxy(StockReservation.StockReservationClient client)
{
    public const string CommitUnavailable = "STOCK_COMMIT_UNAVAILABLE";

    public async Task<CommitResult> CommitAsync(Guid productId, Guid userId, int quantity, CancellationToken ct)
    {
        try
        {
            var reply = await client.CommitAsync(new CommitRequest
            {
                ProductId = productId.ToString(),
                UserId = userId.ToString(),
                Quantity = quantity
            }, cancellationToken: ct);

            return new CommitResult { Success = reply.Success, Code = reply.MessageCode };
        }
        catch (RpcException)
        {
            return new CommitResult { Success = false, Code = CommitUnavailable };
        }
    }
}