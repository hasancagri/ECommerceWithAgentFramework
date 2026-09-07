namespace Storefront.Api.Domains.StorefrontView.Features.Agents;

// 067 US3: agent'a kategori envanteri — YALNIZ satılabilir (dolu-satır + !IsDeleted) ürünlerde fiilen
// kullanılan kategoriler (FR-006). İzole agent slice'ı: facet query'si REUSE edilmez (bilinçli tekrar).
// Cache: kendi verisi + herkese aynı + bayat-toleranslı; "filters" etiketi ProductChangedEvent'te boşalır.
public static class ListCategoriesForAgent
{
    [Cached("filters", 60)]
    public record ListCategoriesQuery();

    public class CategoryItem
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = null!;
        public int ProductCount { get; set; }
    }

    // Saf, test edilebilir çekirdek: distinct kategori + satılabilir ürün sayısı, ada göre sıralı.
    public static List<CategoryItem> Build(IEnumerable<StorefrontView> sellableRows) =>
        sellableRows
            .Where(x => x.CategoryId is not null && !string.IsNullOrWhiteSpace(x.Category))
            .GroupBy(x => x.CategoryId!.Value)
            .Select(g => new CategoryItem
            {
                CategoryId = g.Key,
                Name = g.First().Category!,
                ProductCount = g.Count()
            })
            .OrderBy(x => x.Name)
            .ToList();

    public class ListCategoriesQueryHandler
    {
        public async Task<FeatureListResultModel<CategoryItem>> Handle(
            ListCategoriesQuery query, IQuerySession session, CancellationToken ct)
        {
            var sellable = await session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null)
                .ToListAsync(ct);

            return FeatureListResultModel<CategoryItem>.Ok(Build(sellable));
        }
    }
}