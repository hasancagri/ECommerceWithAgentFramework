using Common;
using Common.Utils.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Domains.Products.Features.Queries;

public static class GetProductByName
{
    [RequiredScope(AuthorizationScopes.CatalogRead)]
    public record GetProductByNameQuery(string Name, int Limit = 5);

    public class GetProductByNameQueryHandler
    {
        public async Task<FeatureObjectResultModel<List<GetProductById.ProductResponse>>> Handle(
            GetProductByNameQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var products = await session.Query<Product>()
                .Where(x => !x.IsDeleted &&
                            x.Name.Contains(query.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name)
                .Take(Math.Clamp(query.Limit, 1, 20))
                .ToListAsync(ct);

            var response = products.Select(GetProductById.ProductResponse.From).ToList();
            return FeatureObjectResultModel<List<GetProductById.ProductResponse>>.Ok(response);
        }
    }
}

public static class GetProductByNameQueryEndpoint
{
    public static RouteGroupBuilder GetProductByNameGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/search", async ([FromQuery] string name, [FromQuery] int? limit, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetProductById.ProductResponse>>>(
                    new GetProductByName.GetProductByNameQuery(name, limit ?? 5));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetProductByName")
            .RequireAuthorization(AuthorizationScopes.CatalogRead);
        return group;
    }
}