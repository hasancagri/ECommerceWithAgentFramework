using Common;
using Common.Utils.Authorization;
using Common.Utils.Constants;

namespace Catalog.Api.Domains.Products.Features.Agent;

public static class GetProduct
{
    [RequiredScope(AuthorizationScopes.CatalogRead)]
    public record GetProductQuery(string Name);

    public class GetProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class GetProductQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetProductResponse>> Handle(
            GetProductQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.Query<Product>()
                .Where(x => !x.IsDeleted && x.IsActive && x.IsComplete &&
                            x.Name.Contains(query.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name)
                .Select(x => new GetProductResponse
                {
                    Id = x.Id,
                    Name = x.Name,
                    Price = x.Price,
                    ImageUrl = x.ImageUrl
                })
                .FirstOrDefaultAsync(ct);

            return FeatureObjectResultModel<GetProductResponse>.Ok(product);
        }
    }
}