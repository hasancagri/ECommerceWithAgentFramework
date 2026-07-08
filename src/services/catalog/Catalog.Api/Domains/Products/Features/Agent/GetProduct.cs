using Common;
using Common.Utils.Authorization;
using Common.Utils.Constants;

namespace Catalog.Api.Domains.Products.Features.Agent;

// MCP 'get_product' tool'unun arkasindaki ajan-ozel sorgu.
// Sepete ekleme akisi icin kullanilir: kullanici bir urunu SEPETE ATMAK isteyince
// isimle aktif urunu bulur ve add_to_cart'a yetecek bilgiyi doner (link YOK).
public static class GetProduct
{
    [RequiredScope(AuthorizationScopes.CatalogRead)]
    public record GetProductQuery(string Name);

    // add_to_cart bu alanlari ister (productId, productName, price, imageUrl).
    // Not: FeatureObjectResultModel<T> 'new()' kisiti istedigi icin parametresiz ctor sart.
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
                .Where(x => !x.IsDeleted && x.IsActive &&
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

            // Ok(null) -> FeatureObjectResultModel otomatik NotFound doner.
            return FeatureObjectResultModel<GetProductResponse>.Ok(product);
        }
    }
}