namespace Discount.Api.Domains.Discounts.Features.Queries;

public static class GetAllProductDiscounts
{
    [RequiredScope(AuthorizationScopes.DiscountRead)]
    public record GetAllProductDiscountsQuery();

    public class ProductDiscountResponse
    {
        public Guid ProductId { get; set; }
        public decimal Rate { get; set; }

        public static ProductDiscountResponse From(Discount discount) => new()
        {
            ProductId = discount.ProductId,
            Rate = discount.Rate.Value
        };
    }

    public class GetAllProductDiscountsQueryHandler
    {
        public async Task<FeatureObjectResultModel<List<ProductDiscountResponse>>> Handle(
            GetAllProductDiscountsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var discounts = await session.Query<Discount>()
                .Where(x => !x.IsDeleted)
                .ToListAsync(ct);

            var response = discounts.Select(ProductDiscountResponse.From).ToList();
            return FeatureObjectResultModel<List<ProductDiscountResponse>>.Ok(response);
        }
    }
}

public static class GetAllProductDiscountsQueryEndpoint
{
    public static RouteGroupBuilder GetAllProductDiscountsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        // 003-storefront-read-model: Storefront'un bootstrap'i (research.md madde 5) toplu okur.
        group.MapGet("/all", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetAllProductDiscounts.ProductDiscountResponse>>>(
                    new GetAllProductDiscounts.GetAllProductDiscountsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("GetAllProductDiscounts")
            .RequireAuthorization(AuthorizationScopes.DiscountRead);
        return group;
    }
}