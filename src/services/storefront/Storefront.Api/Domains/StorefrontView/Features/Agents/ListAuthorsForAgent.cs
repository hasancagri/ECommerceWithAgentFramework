namespace Storefront.Api.Domains.StorefrontView.Features.Agents;

// 067 US3: agent'a yazar envanteri — satılabilir ürünlerde fiilen kullanılan yazarlar (FR-006).
// Binlerce yazar olabilir: search + maxResults + TotalCount ("hepsi bu değil" diyebilsin).
// ProductCount DESC: çok kitaplı yazar önce (keşif değeri). İzole agent slice'ı (bilinçli tekrar).
public static class ListAuthorsForAgent
{
    public const int DefaultMaxResults = 50;
    public const int MaxResultsLimit = 200;

    [Cached("filters", 60)]
    public record ListAuthorsQuery(string? Search = null, int? MaxResults = null);

    public class AuthorItem
    {
        public Guid AuthorId { get; set; }
        public string Name { get; set; } = null!;
        public int ProductCount { get; set; }
    }

    public class ListAuthorsResponse
    {
        public List<AuthorItem> Items { get; set; } = [];
        public int TotalCount { get; set; }
    }

    // Saf çekirdek: Authors düzleştirilir (çok-yazarlı kitap her yazarına sayılır), ada arama filtresi
    // (case-insensitive alt-dizge), ProductCount DESC + Name ASC, kırpma. TotalCount = kırpma ÖNCESİ.
    public static ListAuthorsResponse Build(IEnumerable<StorefrontView> sellableRows, ListAuthorsQuery query)
    {
        var authors = sellableRows
            .SelectMany(x => x.Authors)
            .GroupBy(a => a.Id)
            .Select(g => new AuthorItem { AuthorId = g.Key, Name = g.First().Name, ProductCount = g.Count() });

        if (!string.IsNullOrWhiteSpace(query.Search))
            authors = authors.Where(a =>
                a.Name.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase));

        var filtered = authors.ToList();
        var maxResults = query.MaxResults is null
            ? DefaultMaxResults
            : Math.Clamp(query.MaxResults.Value, 1, MaxResultsLimit);

        return new ListAuthorsResponse
        {
            Items = filtered
                .OrderByDescending(a => a.ProductCount).ThenBy(a => a.Name)
                .Take(maxResults)
                .ToList(),
            TotalCount = filtered.Count
        };
    }

    public class ListAuthorsQueryHandler
    {
        public async Task<FeatureObjectResultModel<ListAuthorsResponse>> Handle(
            ListAuthorsQuery query, IQuerySession session, CancellationToken ct)
        {
            var sellable = await session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null)
                .ToListAsync(ct);

            return FeatureObjectResultModel<ListAuthorsResponse>.Ok(Build(sellable, query));
        }
    }
}