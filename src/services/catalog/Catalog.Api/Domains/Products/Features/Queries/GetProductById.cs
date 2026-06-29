using Common;
using Common.Utils.Constants;
using Shared.Enums;

namespace Catalog.Api.Domains.Products.Features.Queries;

public static class GetProductById
{
    [RequiredScope(AuthorizationScopes.CatalogRead)]
    public record GetProductByIdQuery(Guid Id);

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

    public class GetProductByIdQueryHandler
    {
        public async Task<FeatureObjectResultModel<ProductResponse>> Handle(
            GetProductByIdQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(query.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<ProductResponse>.NotFound();

            return FeatureObjectResultModel<ProductResponse>.Ok(ProductResponse.From(product));
        }
    }
}

public static class GetProductByIdQueryEndpoint
{
    public static RouteGroupBuilder GetProductByIdGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetProductById.ProductResponse>>(
                    new GetProductById.GetProductByIdQuery(id));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetProductById")
            .RequireAuthorization(AuthorizationScopes.CatalogRead);
        return group;
    }
}