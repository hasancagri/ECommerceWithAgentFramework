namespace Stock.Api.Domains.Stocks.Features.Commands;

public static class IncreaseStock
{
    public record IncreaseStockCommand(Guid ProductId, int Amount);

    public class IncreaseStockResponse
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }

    [Transactional]
    public class IncreaseStockCommandHandler
    {
        public async Task<FeatureObjectResultModel<IncreaseStockResponse>> Handle(
            IncreaseStockCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            if (cmd.Amount <= 0)
                return FeatureObjectResultModel<IncreaseStockResponse>.Error(
                    new MessageItem { Code = StockResourceConstants.AMOUNT_MUST_BE_POSITIVE });

            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId, ct);

            if (stock is null)
                return FeatureObjectResultModel<IncreaseStockResponse>.NotFound();

            stock.Increase(cmd.Amount);
            session.Store(stock);

            // 003-storefront-read-model: writer-publishes — Storefront'un StockInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(
                stock.ProductId, stock.Quantity));

            return FeatureObjectResultModel<IncreaseStockResponse>.Ok(new IncreaseStockResponse
            {
                ProductId = stock.ProductId,
                Quantity = stock.Quantity
            });
        }
    }
}

public static class IncreaseStockCommandEndpoint
{
    public static RouteGroupBuilder IncreaseStockGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/increase", async ([FromBody] IncreaseStock.IncreaseStockCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<IncreaseStock.IncreaseStockResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("IncreaseStock")
            .RequireAuthorization(AuthorizationScopes.StockWrite);
        return group;
    }
}