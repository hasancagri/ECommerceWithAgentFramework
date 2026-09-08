namespace Basket.Api.Domains.Baskets.Features.Queries;

// REST ucu söküldü (müşteri yüzeyi MCP-only); slice'ı yalnız checkout gRPC'si tüketir
// (BasketItemsGrpcService). Chat yolu ayrı ikizden gider (GetBasketForAgent).
public static class GetBasket
{
    public record GetBasketQuery(Guid UserId);

    public class GetBasketResponse
    {
        public Guid UserId { get; set; }
        public List<GetBasketItemResponse> Items { get; set; } = new();
        public decimal TotalPrice { get; set; }

        public static GetBasketResponse From(Basket basket) => new()
        {
            UserId = basket.UserId,
            Items = basket.Items.Select(GetBasketItemResponse.From).ToList(),
            TotalPrice = basket.GetTotalPrice()
        };
    }

    public class GetBasketItemResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }

        // 012: adet.
        public int Quantity { get; set; }

        // 021 (FR-007) / 056: satirin ust siniri sabit 5 (stok bileseni yok; stok gercegi checkout'ta).
        // UI + butonunu bu deger'e ulasinca devre disi birakir.
        public int MaxQuantity { get; set; }

        public static GetBasketItemResponse From(BasketItem item) => new()
        {
            Id = item.Id,
            Name = item.Name,
            ImageUrl = item.ImageUrl,
            Price = item.Price,
            Quantity = item.Quantity,
            MaxQuantity = Basket.MaxItemQuantity
        };
    }

    public class GetBasketQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetBasketResponse>> Handle(
            GetBasketQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == query.UserId, ct);

            if (basket is null)
                return FeatureObjectResultModel<GetBasketResponse>.NotFound();

            return FeatureObjectResultModel<GetBasketResponse>.Ok(GetBasketResponse.From(basket));
        }
    }
}