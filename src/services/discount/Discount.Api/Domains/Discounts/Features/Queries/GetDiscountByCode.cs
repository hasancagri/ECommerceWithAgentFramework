
namespace Discount.Api.Domains.Discounts.Features.Queries;

public static class GetDiscountByCode
{
    public record GetDiscountByCodeQuery(string Code);

    public class GetDiscountByCodeResponse
    {
        public string Code { get; set; } = default!;
        public decimal Rate { get; set; }

        public static GetDiscountByCodeResponse From(Discount discount) => new()
        {
            Code = discount.Code.Value,
            Rate = discount.Rate.Value
        };
    }

    public class GetDiscountByCodeQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetDiscountByCodeResponse>> Handle(
            GetDiscountByCodeQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var discount = await session.Query<Discount>()
                .FirstOrDefaultAsync(x => x.Code.Value == query.Code, ct);

            if (discount is null)
                return FeatureObjectResultModel<GetDiscountByCodeResponse>.NotFound();

            return FeatureObjectResultModel<GetDiscountByCodeResponse>.Ok(
                GetDiscountByCodeResponse.From(discount));
        }
    }
}

public static class GetDiscountByCodeEndpoint
{
    public static RouteGroupBuilder GetDiscountByCodeGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{code:length(10)}",
                async (string code, IMessageBus bus) =>
                {
                    var result = await bus.InvokeAsync<FeatureObjectResultModel<GetDiscountByCode.GetDiscountByCodeResponse>>(
                        new GetDiscountByCode.GetDiscountByCodeQuery(code));
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
                })
            .WithName("GetDiscountByCode")
            .MapToApiVersion(1, 0)
            .Produces<GetDiscountByCode.GetDiscountByCodeResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(AuthorizationScopes.DiscountRead);

        return group;
    }
}