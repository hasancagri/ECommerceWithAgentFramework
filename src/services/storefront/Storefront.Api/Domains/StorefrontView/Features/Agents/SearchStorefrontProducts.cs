
namespace Storefront.Api.Domains.StorefrontView.Features.Agents;

// 019: hibrit arama — tek slice, iki yol. SearchText yoksa Marten LINQ + saf in-memory cekirdek
// (FR-008, deterministik Name ASC). SearchText varsa ham SQL: view ⋈ embedding join, kosinus
// mesafesi sirali, esik alti elenir (R3/R6); filtreler her iki yolda da kesindir (FR-007).
public static class SearchStorefrontProductsForAgent
{
    public const int DefaultMaxResults = 8;
    public const int MaxResultsLimit = 20;

    // Kosinus MESAFESI ust siniri (~benzerlik >= 0.3); canli dogrulamada kalibre edilir (R6).
    public const double MaxCosineDistance = 0.7;

    public record SearchStorefrontProductsQuery(
        string[]? Brands = null,
        decimal? MinPrice = null,
        decimal? MaxPrice = null,
        int? MinStock = null,
        string? SearchText = null,
        int? MaxResults = null);

    public static int NormalizeMaxResults(int? maxResults) =>
        maxResults is null ? DefaultMaxResults : Math.Clamp(maxResults.Value, 1, MaxResultsLimit);

    // FR-003 + edge case'ler: en az bir kriter; MinPrice<=MaxPrice; negatif fiyat ve MinStock<1 gecersiz.
    public static List<MessageItem> Validate(SearchStorefrontProductsQuery query)
    {
        var messages = new List<MessageItem>();

        var hasCriteria = query.Brands is { Length: > 0 }
                          || query.MinPrice is not null
                          || query.MaxPrice is not null
                          || query.MinStock is not null
                          || !string.IsNullOrWhiteSpace(query.SearchText);
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

    // Saf, test edilebilir filtre cekirdegi (filtre-yalniz yol): marka OR (case-insensitive tam ad),
    // fiyat araligi dahil, MinStock "en az N" (stogu bilinmeyen satir elenir), Name ASC + kirpma.
    public static List<StorefrontView> FilterAndOrder(
        IEnumerable<StorefrontView> sellableRows, SearchStorefrontProductsQuery query)
    {
        var rows = sellableRows;

        if (query.Brands is { Length: > 0 })
        {
            var brands = query.Brands
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Select(b => b.Trim().ToLowerInvariant())
                .ToHashSet();
            rows = rows.Where(x => x.Brand is not null && brands.Contains(x.Brand.Trim().ToLowerInvariant()));
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
        public string Brand { get; set; } = null!;
        public string? Category { get; set; }
        public decimal Price { get; set; }
        public int? StockQuantity { get; set; }
        public string DetailUrl { get; set; } = null!;

        public static SearchStorefrontProductItem From(StorefrontView view) => new()
        {
            ProductId = view.ProductId,
            Name = view.Name!,
            Brand = view.Brand ?? string.Empty,
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
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            CancellationToken ct)
        {
            var messages = Validate(query);
            if (messages.Count > 0)
                return FeatureListResultModel<SearchStorefrontProductItem>.Error(messages);

            return string.IsNullOrWhiteSpace(query.SearchText)
                ? await SearchByFiltersAsync(query, session, ct)
                : await SearchBySimilarityAsync(query, session, embeddingGenerator, ct);
        }

        // Filtre-yalniz yol: satilabilir satirlar (facet sorgusuyla ayni desen) + saf cekirdek.
        private static async Task<FeatureListResultModel<SearchStorefrontProductItem>> SearchByFiltersAsync(
            SearchStorefrontProductsQuery query, IQuerySession session, CancellationToken ct)
        {
            var sellable = await session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null)
                .ToListAsync(ct);

            var items = FilterAndOrder(sellable, query)
                .Select(SearchStorefrontProductItem.From)
                .ToList();

            return FeatureListResultModel<SearchStorefrontProductItem>.Ok(items);
        }

        // Anlamsal/hibrit yol: Marten LINQ vektor ORDER BY uretemedigi icin ham SQL (R3).
        // Sorgu vektoru TEXT parametre + server-side ::vector cast — pg_type cache yarisina bagisik.
        private static async Task<FeatureListResultModel<SearchStorefrontProductItem>> SearchBySimilarityAsync(
            SearchStorefrontProductsQuery query,
            IQuerySession session,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            CancellationToken ct)
        {
            float[] vector;
            try
            {
                var embedding = await embeddingGenerator.GenerateVectorAsync(query.SearchText!, cancellationToken: ct);
                vector = embedding.ToArray();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Beklenen dis-servis hatasi → hata Result'i (FR-016); filtre-yalniz yol etkilenmez (SC-005).
                return FeatureListResultModel<SearchStorefrontProductItem>.Error(new MessageItem
                {
                    Property = nameof(query.SearchText),
                    Code = StorefrontResourceConstants.STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE
                });
            }

            var filters = new StringBuilder();
            var command = new NpgsqlCommand();

            if (query.Brands is { Length: > 0 })
            {
                filters.Append("\n          and lower(d.data ->> 'Brand') = any(@brands)");
                var brands = query.Brands
                    .Where(b => !string.IsNullOrWhiteSpace(b))
                    .Select(b => b.Trim().ToLowerInvariant())
                    .ToArray();
                command.Parameters.Add(new NpgsqlParameter("brands", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = brands });
            }

            if (query.MinPrice is not null)
            {
                filters.Append("\n          and (d.data ->> 'Price')::numeric >= @minPrice");
                command.Parameters.AddWithValue("minPrice", query.MinPrice.Value);
            }

            if (query.MaxPrice is not null)
            {
                filters.Append("\n          and (d.data ->> 'Price')::numeric <= @maxPrice");
                command.Parameters.AddWithValue("maxPrice", query.MaxPrice.Value);
            }

            if (query.MinStock is not null)
            {
                // ->> null (stok bilinmiyor) karsilastirmada elenir — filtre-yalniz yolla ayni semantik.
                filters.Append("\n          and (d.data ->> 'StockQuantity')::int >= @minStock");
                command.Parameters.AddWithValue("minStock", query.MinStock.Value);
            }

            var vectorLiteral = "[" + string.Join(',', vector.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "]";
            command.Parameters.AddWithValue("queryVector", vectorLiteral);
            command.Parameters.AddWithValue("maxDistance", MaxCosineDistance);
            command.Parameters.AddWithValue("limit", NormalizeMaxResults(query.MaxResults));

            // Embedding'i olmayan urun INNER JOIN geregi sonuca giremez (US2-S3).
            command.CommandText = $"""
                select s.product_id, s.name, s.brand, s.category, s.price, s.stock_quantity
                from (
                    select d.id as product_id,
                           d.data ->> 'Name' as name,
                           d.data ->> 'Brand' as brand,
                           d.data ->> 'Category' as category,
                           (d.data ->> 'Price')::numeric as price,
                           (d.data ->> 'StockQuantity')::int as stock_quantity,
                           (e.data ->> 'Embedding')::vector({ProductEmbedding.Dimensions})
                               <=> cast(@queryVector as vector({ProductEmbedding.Dimensions})) as distance
                    from {SchemaConstants.StorefrontSchemaName}.mt_doc_storefrontview d
                    inner join {SchemaConstants.StorefrontSchemaName}.mt_doc_productembedding e on e.id = d.id
                    where (d.data ->> 'IsDeleted')::boolean = false
                      and d.data ->> 'Name' is not null
                      and d.data ->> 'Price' is not null{filters}
                ) s
                where s.distance <= @maxDistance
                order by s.distance
                limit @limit
                """;

            var connection = session.Connection
                             ?? throw new InvalidOperationException("Marten session bir Npgsql baglantisi saglamadi.");
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(ct);
            command.Connection = connection;

            var items = new List<SearchStorefrontProductItem>();
            await using (command)
            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var productId = reader.GetGuid(0);
                    items.Add(new SearchStorefrontProductItem
                    {
                        ProductId = productId,
                        Name = reader.GetString(1),
                        Brand = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Category = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Price = reader.GetDecimal(4),
                        StockQuantity = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                        DetailUrl = $"/Products/Detail/{productId}"
                    });
                }
            }

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
                string[]? brands = null,
                decimal? minPrice = null,
                decimal? maxPrice = null,
                int? minStock = null,
                string? searchText = null,
                int? maxResults = null) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<SearchStorefrontProductsForAgent.SearchStorefrontProductItem>>(
                    new SearchStorefrontProductsForAgent.SearchStorefrontProductsQuery(
                        brands, minPrice, maxPrice, minStock, searchText, maxResults), ct);

                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("SearchStorefrontProducts")
            .MapToApiVersion(1, 0)
            .Produces<FeatureListResultModel<SearchStorefrontProductsForAgent.SearchStorefrontProductItem>>()
            .AllowAnonymous();

        return group;
    }
}