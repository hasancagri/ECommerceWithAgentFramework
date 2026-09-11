namespace Catalog.Api.Domains.ProductTags.Features.Agents;

// 070 parite: etiket LİSTELEME (agent yüzeyi) — GetProductTags İKİZİ (bilinçli tekrar). Salt-okur:
// scope YOK, audit YOK. Opsiyonel q ile ada göre (case-insensitive alt-dizge) daraltılır; ada göre sıralı.
public static class AdminListProductTagsForAgent
{
    public record ListProductTagsQuery(string? Search = null);

    public class ProductTagItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    public class ListProductTagsQueryHandler
    {
        public async Task<FeatureListResultModel<ProductTagItem>> Handle(
            ListProductTagsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var tags = await session.Query<ProductTag>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            var items = tags.Select(x => new ProductTagItem { Id = x.Id, Name = x.Name });

            if (!string.IsNullOrWhiteSpace(query.Search))
                items = items.Where(t =>
                    t.Name.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase));

            return FeatureListResultModel<ProductTagItem>.Ok(items.ToList());
        }
    }
}
