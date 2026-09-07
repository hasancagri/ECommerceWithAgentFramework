namespace Storefront.Api.Domains.StorefrontView.Features.Agents;

// 067 US3: agent'a yayınevi envanteri — satılabilir ürünlerde fiilen kullanılan yayınevleri (FR-006).
// search + maxResults + TotalCount; ProductCount DESC. İzole agent slice'ı (bilinçli tekrar).
public static class ListPublishersForAgent
{
    public const int DefaultMaxResults = 100;
    public const int MaxResultsLimit = 200;

    [Cached("filters", 60)]
    public record ListPublishersQuery(string? Search = null, int? MaxResults = null);

    public class PublisherItem
    {
        public Guid PublisherId { get; set; }
        public string Name { get; set; } = null!;
        public int ProductCount { get; set; }
    }

    public class ListPublishersResponse
    {
        public List<PublisherItem> Items { get; set; } = [];
        public int TotalCount { get; set; }
    }

    // Saf çekirdek: distinct yayınevi + ürün sayısı, ada arama (case-insensitive alt-dizge),
    // ProductCount DESC + Name ASC, kırpma. TotalCount = kırpma ÖNCESİ.
    public static ListPublishersResponse Build(IEnumerable<StorefrontView> sellableRows, ListPublishersQuery query)
    {
        var publishers = sellableRows
            .Where(x => x.PublisherId is not null && !string.IsNullOrWhiteSpace(x.Publisher))
            .GroupBy(x => x.PublisherId!.Value)
            .Select(g => new PublisherItem
            {
                PublisherId = g.Key,
                Name = g.First().Publisher!,
                ProductCount = g.Count()
            });

        if (!string.IsNullOrWhiteSpace(query.Search))
            publishers = publishers.Where(p =>
                p.Name.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase));

        var filtered = publishers.ToList();
        var maxResults = query.MaxResults is null
            ? DefaultMaxResults
            : Math.Clamp(query.MaxResults.Value, 1, MaxResultsLimit);

        return new ListPublishersResponse
        {
            Items = filtered
                .OrderByDescending(p => p.ProductCount).ThenBy(p => p.Name)
                .Take(maxResults)
                .ToList(),
            TotalCount = filtered.Count
        };
    }

    public class ListPublishersQueryHandler
    {
        public async Task<FeatureObjectResultModel<ListPublishersResponse>> Handle(
            ListPublishersQuery query, IQuerySession session, CancellationToken ct)
        {
            var sellable = await session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null)
                .ToListAsync(ct);

            return FeatureObjectResultModel<ListPublishersResponse>.Ok(Build(sellable, query));
        }
    }
}