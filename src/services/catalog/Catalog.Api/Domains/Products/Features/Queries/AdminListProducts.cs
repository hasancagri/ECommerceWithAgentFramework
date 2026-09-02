namespace Catalog.Api.Domains.Products.Features.Queries;

// 058: admin ürün listesi — draft DAHİL (vitrin filtresi yok; admin gerçek kaynağı görür).
// Arama bellekte (ad contains + ISBN tam eşleşme) — Storefront liste deseniyle aynı ölçek kararı
// (~1.5k kitap; jsonb case-insensitive LIKE kırılganlığına girilmedi).
public static class AdminListProducts
{
    public const int DefaultPageSize = 20;

    public record AdminListProductsQuery(int PageNumber = 1, int PageSize = DefaultPageSize, string? Q = null);

    public class AdminProductListItemResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Isbn { get; set; }
        public decimal Price { get; set; }
        public bool Published { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> AuthorNames { get; set; } = [];
    }

    public class AdminListProductsQueryHandler
    {
        public async Task<FeaturePagedResultModel<AdminProductListItemResponse>> Handle(
            AdminListProductsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
            var pageSize = query.PageSize is < 1 or > 100 ? DefaultPageSize : query.PageSize;

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

            var response = page.Select(p => new AdminProductListItemResponse
            {
                Id = p.Id,
                Name = p.Name,
                Isbn = p.Gtin,
                Price = p.Price.Amount,
                Published = p.Published,
                ImageUrl = p.ImageUrl,
                AuthorNames = p.AuthorIds
                    .Where(authors.ContainsKey)
                    .Select(id => authors[id])
                    .ToList(),
            }).ToList();

            // Boş sonuç NotFound döner; WebApp boş duruma çevirir (011 FR-006 deseni).
            var metaData = new StaticPagedList<AdminProductListItemResponse>(response, pageNumber, pageSize, totalCount);
            return FeaturePagedResultModel<AdminProductListItemResponse>.Ok(metaData, response);
        }
    }
}

public static class AdminListProductsQueryEndpoint
{
    public static RouteGroupBuilder AdminListProductsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/admin", async (IMessageBus bus,
                int page = 1,
                int pageSize = AdminListProducts.DefaultPageSize,
                string? q = null) =>
            {
                var result = await bus.InvokeAsync<FeaturePagedResultModel<AdminListProducts.AdminProductListItemResponse>>(
                    new AdminListProducts.AdminListProductsQuery(page, pageSize, q));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("AdminListProducts")
            .Produces<FeaturePagedResultModel<AdminListProducts.AdminProductListItemResponse>>();
        return group;
    }
}