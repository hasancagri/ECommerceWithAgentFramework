namespace Stock.Api.Domains.Stocks.Features.Queries;

public static class GetStockByProductId
{
    public record GetStockByProductIdQuery(Guid ProductId);

    public class GetStockResponse
    {
        public Guid ProductId { get; set; }
        public int OnHand { get; set; }     // fiziksel stok
        public int Reserved { get; set; }   // aktif rezervasyon
        public int Available { get; set; }  // OnHand - Reserved (>=0); UI "son N adet"

        public static GetStockResponse From(ProductStock stock, DateTimeOffset now) => new()
        {
            ProductId = stock.ProductId,
            OnHand = stock.OnHand,
            Reserved = stock.ReservedAt(now),
            Available = stock.AvailableAt(now)
        };
    }

    public class GetStockByProductIdQueryHandler(ILogger<GetStockByProductIdQueryHandler> logger)
    {
        public async Task<FeatureObjectResultModel<GetStockResponse>> Handle(
            GetStockByProductIdQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == query.ProductId, ct);

            if (stock is null)
                return FeatureObjectResultModel<GetStockResponse>.NotFound();

            var now = DateTimeOffset.UtcNow;

            // G1/FR-017: tedarikci OnHand'i aktif rezervasyonlarin altina dusurmus olabilir (oversell).
            // Otomatik iptal yok; yalniz log. Available zaten 0'a kirpili.
            if (stock.IsOversoldAt(now))
                logger.LogWarning(
                    "Oversell: ProductId={ProductId} OnHand={OnHand} Reserved={Reserved}",
                    stock.ProductId, stock.OnHand, stock.ReservedAt(now));

            return FeatureObjectResultModel<GetStockResponse>.Ok(GetStockResponse.From(stock, now));
        }
    }
}

public static class GetStockByProductIdEndpoint
{
    public static RouteGroupBuilder GetStockByProductIdGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{productId:guid}", async (Guid productId, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetStockByProductId.GetStockResponse>>(
                new GetStockByProductId.GetStockByProductIdQuery(productId));
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
        }).WithName("GetStockByProductId");
        return group;
    }
}