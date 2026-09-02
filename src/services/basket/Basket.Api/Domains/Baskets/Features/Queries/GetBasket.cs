namespace Basket.Api.Domains.Baskets.Features.Queries;

public static class GetBasket
{
    // 057: [RequiredScope] kalkti — anonim yol scope tasiyamaz; sahiplik Guid gizliligiyle korunur.
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

public static class GetBasketQueryEndpoint
{
    public static RouteGroupBuilder GetBasketGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/user", async (HttpContext httpContext, ICurrentUser currentUser, IMessageBus bus) =>
        {
            // 057: anonim erisim — sahip token'dan ya da anonim header'dan.
            var ownerId = BasketEndpointExtension.ResolveOwnerId(httpContext, currentUser);
            if (ownerId == Guid.Empty) return Results.BadRequest();

            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetBasket.GetBasketResponse>>(
                new GetBasket.GetBasketQuery(ownerId));
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
        }).WithName("GetBasket");
        return group;
    }
}