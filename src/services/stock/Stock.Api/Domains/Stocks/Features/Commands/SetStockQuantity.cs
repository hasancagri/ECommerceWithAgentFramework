namespace Stock.Api.Domains.Stocks.Features.Commands;

// 058: admin mutlak stok düzeltmesi ("stok N olsun") — Increase/Decrease'ten ayrı SET semantiği.
// Negatif guard aggregate'te (SetQuantity invariant'ı); stok satırı ProductAdded'dan doğar, burada doğmaz.
public static class SetStockQuantity
{
    public record SetStockQuantityCommand(Guid ProductId, int Quantity);

    public class SetStockQuantityResponse
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }

    [Transactional]
    public class SetStockQuantityCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetStockQuantityResponse>> Handle(
            SetStockQuantityCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId, ct);

            if (stock is null)
                return FeatureObjectResultModel<SetStockQuantityResponse>.NotFound();

            var set = stock.SetQuantity(cmd.Quantity);
            if (!set.IsSuccess)
                return FeatureObjectResultModel<SetStockQuantityResponse>.Error(set.Messages);
            session.Store(stock);

            // 003-storefront-read-model: writer-publishes — Storefront'un StockInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(
                stock.ProductId, stock.Quantity));

            return FeatureObjectResultModel<SetStockQuantityResponse>.Ok(new SetStockQuantityResponse
            {
                ProductId = stock.ProductId,
                Quantity = stock.Quantity
            });
        }
    }
}

public static class SetStockQuantityCommandEndpoint
{
    public static RouteGroupBuilder SetStockQuantityGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/set", async ([FromBody] SetStockQuantity.SetStockQuantityCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<SetStockQuantity.SetStockQuantityResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("SetStockQuantity")
            .RequireAuthorization(AuthorizationScopes.StockWrite);
        return group;
    }
}