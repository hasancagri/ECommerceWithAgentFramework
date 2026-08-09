
namespace Order.Api.Grpc;

public sealed class ClearBasketResult
{
    public bool Success { get; init; }
    public string Code { get; init; } = string.Empty;
}

// 028: saga'nin pivot-sonrasi adimi — Basket ClearBasket gRPC cagrisini sarar. Basarisizlik
// siparisi ETKILEMEZ (FR-009); saga sinirli retry + log ile birakir.
public sealed class BasketClearClientProxy(BasketClear.BasketClearClient client)
{
    public const string ClearUnavailable = "BASKET_CLEAR_UNAVAILABLE";

    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(5);

    public async Task<ClearBasketResult> ClearAsync(Guid userId, Guid orderId, CancellationToken ct)
    {
        try
        {
            var reply = await client.ClearBasketAsync(new ClearBasketRequest
            {
                UserId = userId.ToString(),
                OrderId = orderId.ToString()
            }, deadline: DateTime.UtcNow.Add(CallDeadline), cancellationToken: ct);

            return new ClearBasketResult { Success = reply.Success, Code = reply.MessageCode };
        }
        catch (RpcException)
        {
            return new ClearBasketResult { Success = false, Code = ClearUnavailable };
        }
    }
}