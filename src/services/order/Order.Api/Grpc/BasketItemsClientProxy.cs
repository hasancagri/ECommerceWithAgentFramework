namespace Order.Api.Grpc;

// 039: sepet-snapshot sonucu — kalemler + toplam + deterministik contentHash (correlation-key girdisi).
// Reachable=false ise Basket erisilemez (fail-closed; siparis olusmaz, FR-009).
public sealed record BasketSnapshot(
    bool Reachable,
    IReadOnlyList<CreateOrder.OrderItemDto> Items,
    decimal TotalPrice,
    string ContentHash)
{
    public bool IsEmpty => Items.Count == 0;

    public static readonly BasketSnapshot Unavailable = new(false, [], 0m, "");
}

// 039: chat siparis tamamlama — Basket GetBasketItems gRPC cagrisini sarar (BasketClearClientProxy
// deseni). Kalem sunucu-otoritesi: fiyat/adet buradan gelir, LLM'e girmez. Fail-closed: Basket
// erisilemezse Unavailable doner (siparis olusmaz).
public sealed class BasketItemsClientProxy(BasketQuery.BasketQueryClient client)
{
    private static readonly TimeSpan CallDeadline = TimeSpan.FromSeconds(5);

    public async Task<BasketSnapshot> GetItemsAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var reply = await client.GetBasketItemsAsync(new GetBasketItemsRequest
            {
                UserId = userId.ToString()
            }, deadline: DateTime.UtcNow.Add(CallDeadline), cancellationToken: ct);

            var items = reply.Items
                .Select(l => new CreateOrder.OrderItemDto(
                    Guid.Parse(l.ProductId), l.Name, (decimal)l.UnitPrice, l.Quantity))
                .ToList();

            return new BasketSnapshot(true, items, (decimal)reply.TotalPrice, ComputeContentHash(items));
        }
        catch (RpcException)
        {
            return BasketSnapshot.Unavailable;
        }
    }

    // Deterministik sepet-icerik hash'i: kalemleri ProductId'ye gore sirala, ProductId:Quantity:UnitPrice
    // birlestir, SHA256 hex. Sepet degisince hash degisir -> correlation-key degisir -> yeni cekim dogru.
    private static string ComputeContentHash(IReadOnlyList<CreateOrder.OrderItemDto> items)
    {
        var payload = string.Join("|", items
            .OrderBy(i => i.ProductId)
            .Select(i => $"{i.ProductId:N}:{i.Quantity}:{i.UnitPrice}"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
