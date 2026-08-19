namespace Catalog.Api.Domains.Products.Features.Agents;

public static class SearchProductsForAgent
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
            // 040 FR-007: vitrin kararı Published bayrağında.
            var products = session.Query<Product>()
                .Where(x => !x.IsDeleted && x.Published &&
                            x.Name.Contains(query.Name, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                var normalized = NameNormalization.Normalize(query.Category);
                var category = await session.Query<Category>()
                    .FirstOrDefaultAsync(x => x.NormalizedName == normalized, ct);
                if (category is null)
                    return FeatureObjectResultModel<SearchProductResponse>.Ok(null); // bilinmeyen kategori → sonuç yok

                // 040 K4: kategori artık çoklu atama koleksiyonudur. LINQ Categories.Any(...) KULLANMA:
                // Newtonsoft koleksiyonu $type/$values sarmalayıcısıyla yazar, Marten'ın containment
                // çevirisi düz dizi bekler → 0 eşleşme (canlıda kanıtlandı). Ham JSONB predicate şart.
                products = products.Where(x => x.MatchesSql(
                    "d.data -> 'Categories' -> '$values' @> CAST(? as jsonb)",
                    $"[{{\"CategoryId\": \"{category.Id}\"}}]"));
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