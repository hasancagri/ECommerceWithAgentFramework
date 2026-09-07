
namespace Storefront.Api.Domains.StorefrontView.Features.Agents;

// Vitrin arama — hibrit yol (067): yapısal filtre ÖNCE eler (saf in-memory çekirdek), semanticQuery
// varsa kalan kümede pgvector kosinüs kNN sıralar + mesafe eşiği uygular (alakasız sonuç "benzer
// bulundu" gibi sunulmaz, SC-005). semanticQuery yoksa mevcut deterministik Name ASC davranışı sürer.
public static class SearchStorefrontProductsForAgent
{
    public const int DefaultMaxResults = 8;
    public const int MaxResultsLimit = 20;

    public record SearchStorefrontProductsQuery(
        string[]? Authors = null,
        decimal? MinPrice = null,
        decimal? MaxPrice = null,
        int? MinStock = null,
        int? MaxResults = null,
        // 067: yeni yapısal parametreler + dışlama (FR-002/FR-003) + anlamsal ifade (ham cümle DEĞİL —
        // LLM ayrıştırır: yapısal kısım yapısal parametrelere, bulanık/temalı kısım semanticQuery'ye).
        string? Category = null,
        string? Publisher = null,
        string[]? ExcludeAuthors = null,
        string[]? ExcludePublishers = null,
        string? SemanticQuery = null);

    public static int NormalizeMaxResults(int? maxResults) =>
        maxResults is null ? DefaultMaxResults : Math.Clamp(maxResults.Value, 1, MaxResultsLimit);

    // FR-003 + edge case'ler: en az bir kriter; MinPrice<=MaxPrice; negatif fiyat ve MinStock<1 gecersiz.
    public static List<MessageItem> Validate(SearchStorefrontProductsQuery query)
    {
        var messages = new List<MessageItem>();

        var hasCriteria = query.Authors is { Length: > 0 }
                          || query.MinPrice is not null
                          || query.MaxPrice is not null
                          || query.MinStock is not null
                          || !string.IsNullOrWhiteSpace(query.Category)
                          || !string.IsNullOrWhiteSpace(query.Publisher)
                          || query.ExcludeAuthors is { Length: > 0 }
                          || query.ExcludePublishers is { Length: > 0 }
                          || !string.IsNullOrWhiteSpace(query.SemanticQuery);
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

    // pgvector metin formu: "[0.1,0.2,...]" (InvariantCulture şart — virgül/nokta karışmasın).
    public static string ToVectorLiteral(ReadOnlySpan<float> vector)
    {
        var parts = new string[vector.Length];
        for (var i = 0; i < vector.Length; i++)
            parts[i] = vector[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"[{string.Join(',', parts)}]";
    }

    // Ad eşleşmesi noktalama/boşluk DUYARSIZ: yalnız harf+rakam, lowercase — "H.G. Wells" = "H. G. Wells"
    // (canlı bulgu: tam-ad eşleşmesi kullanıcı yazımını ıskalıyordu). Yazar/yayınevi/kategori aynı kuralı kullanır.
    public static string NormalizeName(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static HashSet<string> NormalizeNames(string[] values) =>
        values.Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(NormalizeName)
            .ToHashSet();

    // Saf, test edilebilir YAPISAL filtre cekirdegi (067: sira/kirpmasiz — semantik yol ham kumeyi ister).
    // Yazar OR + yazar/yayinevi DISLAMA + kategori/yayinevi esitligi (case-insensitive tam ad),
    // fiyat araligi dahil, MinStock "en az N" (stogu bilinmeyen satir elenir).
    public static IEnumerable<StorefrontView> Filter(
        IEnumerable<StorefrontView> sellableRows, SearchStorefrontProductsQuery query)
    {
        var rows = sellableRows;

        if (query.Authors is { Length: > 0 })
        {
            // 052: yazar adı OR. Çok-yazarlı kitap herhangi bir yazarı uyarsa eşleşir (normalize eşleşme).
            var authors = NormalizeNames(query.Authors);
            rows = rows.Where(x => x.Authors.Any(a => authors.Contains(NormalizeName(a.Name))));
        }

        // 067 FR-003: dışlama — herhangi bir yazarı listede olan kitap elenir.
        if (query.ExcludeAuthors is { Length: > 0 })
        {
            var excluded = NormalizeNames(query.ExcludeAuthors);
            rows = rows.Where(x => !x.Authors.Any(a => excluded.Contains(NormalizeName(a.Name))));
        }

        if (!string.IsNullOrWhiteSpace(query.Publisher))
        {
            var publisher = NormalizeName(query.Publisher);
            rows = rows.Where(x => x.Publisher is not null && NormalizeName(x.Publisher) == publisher);
        }

        if (query.ExcludePublishers is { Length: > 0 })
        {
            var excluded = NormalizeNames(query.ExcludePublishers);
            rows = rows.Where(x => x.Publisher is null || !excluded.Contains(NormalizeName(x.Publisher)));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = NormalizeName(query.Category);
            rows = rows.Where(x => x.Category is not null && NormalizeName(x.Category) == category);
        }

        if (query.MinPrice is not null)
            rows = rows.Where(x => x.Price >= query.MinPrice);
        if (query.MaxPrice is not null)
            rows = rows.Where(x => x.Price <= query.MaxPrice);
        if (query.MinStock is not null)
            rows = rows.Where(x => x.StockQuantity >= query.MinStock);

        return rows;
    }

    // Yapısal-yalnız yol: deterministik Name ASC + kirpma (mevcut davranış korunur).
    public static List<StorefrontView> FilterAndOrder(
        IEnumerable<StorefrontView> sellableRows, SearchStorefrontProductsQuery query) =>
        Filter(sellableRows, query)
            .OrderBy(x => x.Name)
            .Take(NormalizeMaxResults(query.MaxResults))
            .ToList();

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

    // 067 kontrat #1: Found=false → LLM "bulunamadı" der (boş listeyi başarı gibi sunmaz).
    public class SearchStorefrontProductsResponse
    {
        public bool Found { get; set; }
        public List<SearchStorefrontProductItem> Items { get; set; } = [];
    }

    public class SearchStorefrontProductsQueryHandler
    {
        public async Task<FeatureObjectResultModel<SearchStorefrontProductsResponse>> Handle(
            SearchStorefrontProductsQuery query,
            IQuerySession session,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            SemanticSearchOption semanticOptions,
            CancellationToken ct)
        {
            var messages = Validate(query);
            if (messages.Count > 0)
                return FeatureObjectResultModel<SearchStorefrontProductsResponse>.Error(messages);

            var sellable = await session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null)
                .ToListAsync(ct);

            List<StorefrontView> resultRows;
            if (string.IsNullOrWhiteSpace(query.SemanticQuery))
            {
                resultRows = FilterAndOrder(sellable, query);
            }
            else
            {
                // 067 hibrit yol (research R2): yapısal ÖNCE eler → kalan id'lerde kNN + eşik.
                var candidates = Filter(sellable, query).ToDictionary(x => x.ProductId);
                if (candidates.Count == 0)
                    return FeatureObjectResultModel<SearchStorefrontProductsResponse>.Ok(
                        new SearchStorefrontProductsResponse { Found = false });

                ReadOnlyMemory<float> queryVector;
                try
                {
                    queryVector = await embeddingGenerator.GenerateVectorAsync(
                        query.SemanticQuery, cancellationToken: ct);
                }
                catch (Exception)
                {
                    // 019 sabiti: embedding servisi erişilemez — beklenen hata, Result ile taşınır.
                    return FeatureObjectResultModel<SearchStorefrontProductsResponse>.Error(
                    [
                        new MessageItem
                        {
                            Property = nameof(query.SemanticQuery),
                            Code = StorefrontResourceConstants.STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE
                        }
                    ]);
                }

                // Temsil AYRI dokümanda; kNN yalnız temsili olan adaylar üzerinde koşar (açıklamasız
                // ürün semantik aday değildir — edge case). Eşik SQL WHERE'de (SC-005).
                // Vektör parametresi METİN literal + CAST — Weasel, Pgvector.Vector tipini bind edemiyor
                // (canlı bulgu: "Can't infer NpgsqlDbType for type Pgvector.Vector").
                var vectorLiteral = ToVectorLiteral(queryVector.Span);
                var ordered = await session.QueryAsync<ProductDescriptionEmbedding>(
                    "where id = ANY(?) and (data ->> 'Vector')::vector <=> CAST(? as vector) < ? " +
                    "order by (data ->> 'Vector')::vector <=> CAST(? as vector) limit ?",
                    ct,
                    candidates.Keys.ToArray(),
                    vectorLiteral,
                    semanticOptions.MaxCosineDistance,
                    vectorLiteral,
                    NormalizeMaxResults(query.MaxResults));

                resultRows = ordered.Select(e => candidates[e.ProductId]).ToList();
            }

            return FeatureObjectResultModel<SearchStorefrontProductsResponse>.Ok(
                new SearchStorefrontProductsResponse
                {
                    Found = resultRows.Count > 0,
                    Items = resultRows.Select(SearchStorefrontProductItem.From).ToList()
                });
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
                int? maxResults = null,
                string? category = null,
                string? publisher = null,
                string[]? excludeAuthors = null,
                string[]? excludePublishers = null,
                string? semanticQuery = null) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<SearchStorefrontProductsForAgent.SearchStorefrontProductsResponse>>(
                    new SearchStorefrontProductsForAgent.SearchStorefrontProductsQuery(
                        authors, minPrice, maxPrice, minStock, maxResults,
                        category, publisher, excludeAuthors, excludePublishers, semanticQuery), ct);

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("SearchStorefrontProducts")
            .MapToApiVersion(1, 0)
            .Produces<FeatureObjectResultModel<SearchStorefrontProductsForAgent.SearchStorefrontProductsResponse>>()
            .AllowAnonymous();

        return group;
    }
}