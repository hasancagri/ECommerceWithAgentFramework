namespace Catalog.Api.Domains.Products.Features.Agent;

public static class SearchProducts
{
    // 016: opsiyonel kategori/marka daraltması (FR-012) — ad normalize edilip Id'ye çözülür, filtre Id ile.
    public record SearchProductsQuery(string Name, string? Category = null, string? Brand = null);

    public class SearchProductResponse
    {
        public string DetailUrl { get; set; } = null!;
    }

    public class SearchProductsQueryHandler
    {
        public async Task<FeatureObjectResultModel<SearchProductResponse>> Handle(
            SearchProductsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var products = session.Query<Product>()
                .Where(x => !x.IsDeleted && x.IsActive &&
                            x.Name.Contains(query.Name, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                var normalized = NameNormalization.Normalize(query.Category);
                var category = await session.Query<Category>()
                    .FirstOrDefaultAsync(x => x.NormalizedName == normalized, ct);
                if (category is null)
                    return FeatureObjectResultModel<SearchProductResponse>.Ok(null); // bilinmeyen kategori → sonuç yok

                products = products.Where(x => x.CategoryId == category.Id);
            }

            if (!string.IsNullOrWhiteSpace(query.Brand))
            {
                var normalized = NameNormalization.Normalize(query.Brand);
                var brand = await session.Query<Brand>()
                    .FirstOrDefaultAsync(x => x.NormalizedName == normalized, ct);
                if (brand is null)
                    return FeatureObjectResultModel<SearchProductResponse>.Ok(null); // bilinmeyen marka → sonuç yok

                products = products.Where(x => x.BrandId == brand.Id);
            }

            var row = await products
                .OrderBy(x => x.Name)
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync(ct);

            var response = row is null
                ? null
                : new SearchProductResponse { DetailUrl = $"/Products/Detail/{row.Id}" };

            return FeatureObjectResultModel<SearchProductResponse>.Ok(response);
        }
    }
}