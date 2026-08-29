
namespace Storefront.Api.Domains.StorefrontView.Features.Agents;

// Vitrin arama — yapısal filtre yolu: Marten LINQ + saf in-memory çekirdek (deterministik Name ASC).
// Filtreler kesindir (yazar OR, fiyat aralığı, asgari stok). Anlamsal/embedding yolu söküldü.
public static class SearchStorefrontProductsForAgent
{
    public const int DefaultMaxResults = 8;
    public const int MaxResultsLimit = 20;

    public record SearchStorefrontProductsQuery(
        string[]? Authors = null,
        decimal? MinPrice = null,
        decimal? MaxPrice = null,
        int? MinStock = null,
        int? MaxResults = null);

    public static int NormalizeMaxResults(int? maxResults) =>
        maxResults is null ? DefaultMaxResults : Math.Clamp(maxResults.Value, 1, MaxResultsLimit);

    // FR-003 + edge case'ler: en az bir kriter; MinPrice<=MaxPrice; negatif fiyat ve MinStock<1 gecersiz.
    public static List<MessageItem> Validate(SearchStorefrontProductsQuery query)
    {
        var messages = new List<MessageItem>();

        var hasCriteria = query.Authors is { Length: > 0 }
                          || query.MinPrice is not null
                          || query.MaxPrice is not null
                          || query.MinStock is not null;
        if (!hasCriteria)
        {
            messages.Add(new MessageItem
            {
                Property = nameof(SearchStorefrontProductsQuery),
                Code = StorefrontResourceConstants.VALUE_IS_REQUIRED
            });
            return messages;
        }

        if (query.MinPrice is < 0)
            messages.Add(new MessageItem
            {
                Property = nameof(query.MinPrice),
                Code = StorefrontResourceConstants.INVALID_VALUE
            });

        if (query.MaxPrice is < 0)
            messages.Add(new MessageItem
            {
                Property = nameof(query.MaxPrice),
                Code = StorefrontResourceConstants.INVALID_VALUE
            });

        if (query is { MinPrice: >= 0, MaxPrice: >= 0 } && query.MinPrice > query.MaxPrice)
            messages.Add(new MessageItem
            {
                Property = nameof(query.MinPrice),
                Code = StorefrontResourceConstants.INVALID_RANGE
            });

        if (query.MinStock is < 1)
            messages.Add(new MessageItem
            {
                Property = nameof(query.MinStock),
                Code = StorefrontResourceConstants.INVALID_VALUE
            });

        return messages;
    }

    // Saf, test edilebilir filtre cekirdegi: marka OR (case-insensitive tam ad), fiyat araligi dahil,
    // MinStock "en az N" (stogu bilinmeyen satir elenir), Name ASC + kirpma.
    public static List<StorefrontView> FilterAndOrder(
        IEnumerable<StorefrontView> sellableRows, SearchStorefrontProductsQuery query)
    {
        var rows = sellableRows;

        if (query.Authors is { Length: > 0 })
        {
            // 052: yazar adı OR (case-insensitive tam ad). Çok-yazarlı kitap herhangi bir yazarı uyarsa eşleşir.
            var authors = query.Authors
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim().ToLowerInvariant())
                .ToHashSet();
            rows = rows.Where(x => x.Authors.Any(a => authors.Contains(a.Name.Trim().ToLowerInvariant())));
        }

        if (query.MinPrice is not null)
            rows = rows.Where(x => x.Price >= query.MinPrice);
        if (query.MaxPrice is not null)
            rows = rows.Where(x => x.Price <= query.MaxPrice);
        if (query.MinStock is not null)
            rows = rows.Where(x => x.StockQuantity >= query.MinStock);

        return rows
            .OrderBy(x => x.Name)
            .Take(NormalizeMaxResults(query.MaxResults))
            .ToList();
    }

    public class SearchStorefrontProductItem
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = null!;
        // 052: künye — yazar adları + tek yayınevi (eski tek Brand alanının yerine).
        public string[] Authors { get; set; } = [];
        public string? Publisher { get; set; }
        public string? Category { get; set; }
        public decimal Price { get; set; }
        public int? StockQuantity { get; set; }
        public string DetailUrl { get; set; } = null!;

        public static SearchStorefrontProductItem From(StorefrontView view) => new()
        {
            ProductId = view.ProductId,
            Name = view.Name!,
            Authors = view.Authors.Select(a => a.Name).ToArray(),
            Publisher = view.Publisher,
            Category = view.Category,
            Price = view.Price!.Value,
            StockQuantity = view.StockQuantity,
            DetailUrl = $"/Products/Detail/{view.ProductId}" // FR-010: Catalog search_products ile ayni bicim
        };
    }

    public class SearchStorefrontProductsQueryHandler
    {
        public async Task<FeatureListResultModel<SearchStorefrontProductItem>> Handle(
            SearchStorefrontProductsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var messages = Validate(query);
            if (messages.Count > 0)
                return FeatureListResultModel<SearchStorefrontProductItem>.Error(messages);

            var sellable = await session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null)
                .ToListAsync(ct);

            var items = FilterAndOrder(sellable, query)
                .Select(SearchStorefrontProductItem.From)
                .ToList();

            return FeatureListResultModel<SearchStorefrontProductItem>.Ok(items);
        }
    }
}

public static class SearchStorefrontProductsEndpoint
{
    // R8: ayni query'nin anonim REST yuzu — canli dogrulama sohbet LLM'inden bagimsiz olur.
    public static RouteGroupBuilder SearchStorefrontProductsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/search", async (IMessageBus bus,
                CancellationToken ct,
                string[]? authors = null,
                decimal? minPrice = null,
                decimal? maxPrice = null,
                int? minStock = null,
                int? maxResults = null) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<SearchStorefrontProductsForAgent.SearchStorefrontProductItem>>(
                    new SearchStorefrontProductsForAgent.SearchStorefrontProductsQuery(
                        authors, minPrice, maxPrice, minStock, maxResults), ct);

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("SearchStorefrontProducts")
            .MapToApiVersion(1, 0)
            .Produces<FeatureListResultModel<SearchStorefrontProductsForAgent.SearchStorefrontProductItem>>()
            .AllowAnonymous();

        return group;
    }
}