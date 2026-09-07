namespace Catalog.Api.Domains.Publishers.Features.Agents;

// 067 taşıma (kullanıcı kararı): yayınevi envanteri Publisher aggregate'inin evinde. Yalnız YAYINDAKİ
// en az bir üründe geçen yayınevleri (FR-006 ruhu). search + maxResults + TotalCount; çok kitaplı önce.
public static class ListPublishersForAgent
{
    public const int DefaultMaxResults = 100;
    public const int MaxResultsLimit = 200;

    [Cached("agent-lists", 60)]
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

    // Saf çekirdek: yayındaki ürünlerin PublisherId'sinden sayım, ada arama, ProductCount DESC + Name ASC.
    public static ListPublishersResponse Build(
        IEnumerable<Products.Product> publishedProducts, IReadOnlyList<Publisher> allPublishers,
        ListPublishersQuery query)
    {
        var byId = allPublishers.ToDictionary(p => p.Id);

        var publishers = publishedProducts
            .GroupBy(p => p.PublisherId)
            .Where(g => byId.ContainsKey(g.Key))
            .Select(g => new PublisherItem { PublisherId = g.Key, Name = byId[g.Key].Name, ProductCount = g.Count() });

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
            var products = await session.Query<Products.Product>()
                .Where(x => x.Published).ToListAsync(ct);
            var publishers = await session.Query<Publisher>().ToListAsync(ct);

            return FeatureObjectResultModel<ListPublishersResponse>.Ok(Build(products, publishers, query));
        }
    }
}