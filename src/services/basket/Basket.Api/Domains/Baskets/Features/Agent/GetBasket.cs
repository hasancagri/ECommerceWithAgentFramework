namespace Basket.Api.Domains.Baskets.Features.Agent;

public static class GetBasket
{
    public record GetBasketQuery(Guid UserId);

    public class GetBasketResponse
    {
        public Guid UserId { get; set; }
        public List<GetBasketItemResponse> Items { get; set; } = new();
        public float? DiscountRate { get; set; }
        public string? Coupon { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal? TotalPriceWithAppliedDiscount { get; set; }

        public static GetBasketResponse From(Basket basket) => new()
        {
            UserId = basket.UserId,
            Items = basket.Items.Select(GetBasketItemResponse.From).ToList(),
            DiscountRate = basket.AppliedDiscount?.Rate,
            Coupon = basket.AppliedDiscount?.Coupon,
            TotalPrice = basket.GetTotalPrice(),
            TotalPriceWithAppliedDiscount = basket.GetTotalPriceWithAppliedDiscount()
        };
    }

    public class GetBasketItemResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public decimal? PriceByApplyDiscountRate { get; set; }

        public static GetBasketItemResponse From(BasketItem item) => new()
        {
            Id = item.Id,
            Name = item.Name,
            ImageUrl = item.ImageUrl,
            Price = item.Price,
            PriceByApplyDiscountRate = item.PriceByApplyDiscountRate
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