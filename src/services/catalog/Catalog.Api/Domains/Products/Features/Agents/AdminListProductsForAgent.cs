namespace Catalog.Api.Domains.Products.Features.Agents;

// 070 US1: admin ürün listesi (agent yüzeyi) — AdminListProducts İKİZİ (bilinçli tekrar; agent
// slice Commands/Queries'e IMessageBus ile bile gitmez). Draft DAHİL; arama bellekte (~1.5k kitap).
// Okuma tool'u — AdminActionLog yazılmaz (FR-009 yalnız yazma).
public static class AdminListProductsForAgent
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 50;

    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminListProductsQuery(int Page = 1, int PageSize = DefaultPageSize, string? Q = null);

    public class AdminProductListItem
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = default!;
        public string? Isbn { get; set; }
        public decimal Price { get; set; }
        public bool IsPublished { get; set; }
        public List<string> AuthorNames { get; set; } = [];
    }

    public class AdminListProductsResponse
    {
        public List<AdminProductListItem> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class AdminListProductsForAgentQueryHandler
    {
        public async Task<FeatureObjectResultModel<AdminListProductsResponse>> Handle(
            AdminListProductsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var pageNumber = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize is < 1 or > MaxPageSize ? DefaultPageSize : query.PageSize;

            var products = await session.Query<Product>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var q = query.Q.Trim();
                products = products
                    .Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Gtin == q)
                    .ToList();
            }

            var totalCount = products.Count;
            var page = products
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Sayfadaki yazar adları tek seferde çözülür (satır başına lookup yok).
            var authorIds = page.SelectMany(p => p.AuthorIds).Distinct().ToArray();
            var authors = (await session.LoadManyAsync<Author>(ct, authorIds))
                .ToDictionary(a => a.Id, a => a.Name);

            return FeatureObjectResultModel<AdminListProductsResponse>.Ok(new AdminListProductsResponse
            {
                Items = page.Select(p => new AdminProductListItem
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    Isbn = p.Gtin,
                    Price = p.Price.Amount,
                    IsPublished = p.Published,
                    AuthorNames = p.AuthorIds
                        .Where(authors.ContainsKey)
                        .Select(id => authors[id])
                        .ToList(),
                }).ToList(),
                TotalCount = totalCount,
                Page = pageNumber,
                PageSize = pageSize,
            });
        }
    }
}