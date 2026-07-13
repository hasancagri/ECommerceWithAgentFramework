namespace Catalog.Api.Domains.Products.Features.Agent;

// Enrichment adaylari: satisa-hazir olmayan (aciklama ve/veya gorsel eksik) urunler (FR-001).
public static class ListIncompleteProducts
{
    [RequiredScope(AuthorizationScopes.CatalogRead)]
    public record ListIncompleteProductsQuery(int Limit = 100);

    public class IncompleteProductItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public BrandType Brand { get; set; }
        public bool HasDescription { get; set; }
        public bool HasImage { get; set; }
    }

    public class ListIncompleteProductsQueryHandler
    {
        public async Task<FeatureListResultModel<IncompleteProductItem>> Handle(
            ListIncompleteProductsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var products = await session.Query<Product>()
                .Where(x => !x.IsDeleted && !x.IsComplete)
                .OrderBy(x => x.Name)
                .Take(query.Limit)
                .ToListAsync(ct);

            var items = products.Select(x => new IncompleteProductItem
            {
                Id = x.Id,
                Name = x.Name,
                Brand = x.Brand,
                HasDescription = !string.IsNullOrWhiteSpace(x.Description),
                HasImage = !string.IsNullOrWhiteSpace(x.ImageUrl),
            }).ToList();

            return FeatureListResultModel<IncompleteProductItem>.Ok(items);
        }
    }
}