using Common;
using Common.Utils.Authorization;
using Common.Utils.Constants;
using Microsoft.AspNetCore.Mvc;
using Shared.Enums;

namespace Catalog.Api.Domains.Products.Features.Queries;

public static class GetProductByName
{
    [RequiredScope(AuthorizationScopes.CatalogRead)]
    public record GetProductByNameQuery(string Name);
    
    public class ProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }
        public string Sku { get; set; } = null!;
        public BrandType Brand { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }

        public static ProductResponse From(Product p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Sku = p.Sku,
            Brand = p.Brand,
            ImageUrl = p.ImageUrl,
            IsActive = p.IsActive
        };
    }

    public class GetProductByNameQueryHandler
    {
        public async Task<FeatureObjectResultModel<ProductResponse>> Handle(
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
            return FeatureObjectResultModel<GetProductByName.ProductResponse>.Ok(
                product is null ? null : ProductResponse.From(product));
        }
    }
}

public static class GetProductByNameQueryEndpoint
{
    public static RouteGroupBuilder GetProductByNameGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/search", async ([FromQuery] string name, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetProductByName.ProductResponse>>(
                    new GetProductByName.GetProductByNameQuery(name));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetProductByName")
            .RequireAuthorization(AuthorizationScopes.CatalogRead);
        return group;
    }
}