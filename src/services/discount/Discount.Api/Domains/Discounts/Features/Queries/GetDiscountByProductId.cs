namespace Discount.Api.Domains.Discounts.Features.Queries;

public static class GetDiscountByProductId
{
    public record GetDiscountByProductIdQuery(Guid ProductId);

    public class GetDiscountByProductIdResponse
    {
        public Guid ProductId { get; set; }
        public decimal Rate { get; set; }

        public static GetDiscountByProductIdResponse From(Discount discount) => new()
        {
            ProductId = discount.ProductId,
            Rate = discount.Rate.Value
        };
    }

    public class GetDiscountByProductIdQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetDiscountByProductIdResponse>> Handle(
            GetDiscountByProductIdQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var discount = await session.Query<Discount>()
                .FirstOrDefaultAsync(x => x.ProductId == query.ProductId && !x.IsDeleted, ct);

            if (discount is null)
                return FeatureObjectResultModel<GetDiscountByProductIdResponse>.NotFound();

            return FeatureObjectResultModel<GetDiscountByProductIdResponse>.Ok(
                GetDiscountByProductIdResponse.From(discount));
        }
    }
}

public static class GetDiscountByProductIdEndpoint
{
    public static RouteGroupBuilder GetDiscountByProductIdGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{productId:guid}",
                async (Guid productId, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<GetDiscountByProductId.GetDiscountByProductIdResponse>>(
                        new GetDiscountByProductId.GetDiscountByProductIdQuery(productId));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("GetDiscountByProductId")
            .MapToApiVersion(1, 0)
            .Produces<GetDiscountByProductId.GetDiscountByProductIdResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }
}