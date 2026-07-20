namespace Stock.Api.Domains.Stocks.Features.Commands;

public static class DecreaseStock
{
    public record DecreaseStockCommand(Guid ProductId, int Amount);

    public class DecreaseStockResponse
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }

    [Transactional]
    public class DecreaseStockCommandHandler
    {
        public async Task<FeatureObjectResultModel<DecreaseStockResponse>> Handle(
            DecreaseStockCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            if (cmd.Amount <= 0)
                return FeatureObjectResultModel<DecreaseStockResponse>.Error(
                    new MessageItem { Code = "AMOUNT_MUST_BE_POSITIVE" });

            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId, ct);

            if (stock is null)
                return FeatureObjectResultModel<DecreaseStockResponse>.NotFound();

            if (stock.Quantity < cmd.Amount)
                return FeatureObjectResultModel<DecreaseStockResponse>.Error(
                    new MessageItem { Code = "INSUFFICIENT_STOCK" });

            stock.Decrease(cmd.Amount);
            session.Store(stock);

            // 003-storefront-read-model: writer-publishes — Storefront'un StockInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(
                stock.ProductId, stock.Quantity));

            return FeatureObjectResultModel<DecreaseStockResponse>.Ok(new DecreaseStockResponse
            {
                ProductId = stock.ProductId,
                Quantity = stock.Quantity
            });
        }
    }
}

public static class DecreaseStockCommandEndpoint
{
    public static RouteGroupBuilder DecreaseStockGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/decrease", async ([FromBody] DecreaseStock.DecreaseStockCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<DecreaseStock.DecreaseStockResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("DecreaseStock")
            .RequireAuthorization(AuthorizationScopes.StockWrite);
        return group;
    }
}