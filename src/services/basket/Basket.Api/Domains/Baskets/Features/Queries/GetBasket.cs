namespace Basket.Api.Domains.Baskets.Features.Queries;

public static class GetBasket
{
    [RequiredScope(AuthorizationScopes.BasketRead)]
    public record GetBasketQuery(Guid UserId);

    public class GetBasketResponse
    {
        public Guid UserId { get; set; }
        public List<GetBasketItemResponse> Items { get; set; } = new();
        public float? DiscountRate { get; set; }
        public string? Coupon { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal? TotalPriceWithAppliedDiscount { get; set; }

        // 017: sepet capasi + turetilmis dolma durumu (FR-009/010). Null capa => UI banner gostermez.
        public DateTimeOffset? ReservationExpiresAt { get; set; }
        public bool IsReservationExpired { get; set; }

        public static GetBasketResponse From(Basket basket) => new()
        {
            UserId = basket.UserId,
            Items = basket.Items.Select(GetBasketItemResponse.From).ToList(),
            DiscountRate = basket.AppliedDiscount?.Rate,
            Coupon = basket.AppliedDiscount?.Coupon,
            TotalPrice = basket.GetTotalPrice(),
            TotalPriceWithAppliedDiscount = basket.GetTotalPriceWithAppliedDiscount(),
            ReservationExpiresAt = basket.ReservationExpiresAt,
            IsReservationExpired = basket.IsExpiredAt(DateTimeOffset.UtcNow)
        };
    }

    public class GetBasketItemResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public decimal? PriceByApplyDiscountRate { get; set; }

        // 012: adet. Rezervasyon bitisi artik sepet duzeyinde (017).
        public int Quantity { get; set; }

        public static GetBasketItemResponse From(BasketItem item) => new()
        {
            Id = item.Id,
            Name = item.Name,
            ImageUrl = item.ImageUrl,
            Price = item.Price,
            PriceByApplyDiscountRate = item.PriceByApplyDiscountRate,
            Quantity = item.Quantity
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
        group.MapGet("/user", async (HttpContext httpContext, IMessageBus bus) =>
        {
            var userId = CurrentUser.Load(httpContext.User).Id;
            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetBasket.GetBasketResponse>>(
                new GetBasket.GetBasketQuery(userId));
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
        }).WithName("GetBasket")
            .RequireAuthorization(AuthorizationScopes.BasketRead);
        return group;
    }
}