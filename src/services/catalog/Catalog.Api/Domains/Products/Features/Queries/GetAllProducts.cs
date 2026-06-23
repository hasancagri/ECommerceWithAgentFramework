using Common;
using Common.Utils.Constants;

namespace Catalog.Api.Domains.Products.Features.Queries;

public static class GetAllProducts
{
    public record GetAllProductsQuery();

    public class GetAllProductsQueryHandler
    {
        public async Task<FeatureObjectResultModel<List<GetProductById.ProductResponse>>> Handle(
            GetAllProductsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var products = await session.Query<Product>()
                .Where(x => !x.IsDeleted)
                .ToListAsync(ct);

            var response = products.Select(GetProductById.ProductResponse.From).ToList();
            return FeatureObjectResultModel<List<GetProductById.ProductResponse>>.Ok(response);
        }
    }
}

public static class GetAllProductsQueryEndpoint
{
    public static RouteGroupBuilder GetAllProductsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetProductById.ProductResponse>>>(
                    new GetAllProducts.GetAllProductsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("GetAllProducts")
            .RequireAuthorization(AuthorizationScopes.CatalogRead);
        return group;
    }
}