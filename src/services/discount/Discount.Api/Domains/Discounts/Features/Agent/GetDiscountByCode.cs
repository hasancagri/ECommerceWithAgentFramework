using Common.Utils.Authorization;

namespace Discount.Api.Domains.Discounts.Features.Agent;

// MCP (agent) tool'una ozel: REST Query'sinden bagimsiz; response ileride ayri evrilebilsin diye ayri tutuldu.
public static class GetDiscountByCode
{
    [RequiredScope(AuthorizationScopes.DiscountRead)]
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