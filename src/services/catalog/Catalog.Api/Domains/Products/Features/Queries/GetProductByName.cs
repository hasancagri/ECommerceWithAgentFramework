using Common;
using Common.Utils.Authorization;
using Common.Utils.Constants;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Domains.Products.Features.Queries;

public static class GetProductByName
{
    [RequiredScope(AuthorizationScopes.CatalogRead)]
    public record GetProductByNameQuery(string Name);

    public class GetProductByNameQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetProductById.ProductResponse>> Handle(
            GetProductByNameQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Isme gore (kismi, buyuk/kucuk harf duyarsiz) en iyi eslesen TEK urun.
            var product = await session.Query<Product>()
                .Where(x => !x.IsDeleted &&
                            x.Name.Contains(query.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name)
                .FirstOrDefaultAsync(ct);

            // Ok(null) -> FeatureObjectResultModel otomatik NotFound doner.
            return FeatureObjectResultModel<GetProductById.ProductResponse>.Ok(
                product is null ? null : GetProductById.ProductResponse.From(product));
        }
    }
}

public static class GetProductByNameQueryEndpoint
{
    public static RouteGroupBuilder GetProductByNameGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/search", async ([FromQuery] string name, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetProductById.ProductResponse>>(
                    new GetProductByName.GetProductByNameQuery(name));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetProductByName")
            .RequireAuthorization(AuthorizationScopes.CatalogRead);
        return group;
    }
}