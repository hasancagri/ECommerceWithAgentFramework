namespace Storefront.Api.Domains.StorefrontView.Features.Queries;

// 053 US1: ranking query slice (İlke III — IQuerySession doğrudan, repository yok). Bir KUŞAĞIN öznitelik
// ağırlıklarını gövdede alır → o kuşak için sıralı kitap kartları. WebApp her cluster + discovery için
// ayrı çağırır (share → pageSize payı). Skor + sıralama saf (RecommendationScoring); aday çekme burada.
public static class GetRecommendedProducts
{
    public const int DefaultPageSize = 12;

    // MMR çeşitlendirme λ (0.7 = ilgiye ağırlıklı, biraz çeşitlilik). Canlı gözlemle ayarlanır (R7/config).
    private const decimal MmrLambda = 0.7m;

    public record AttributeWeightInput(string Type, string Value, decimal Weight);

    public record GetRecommendedProductsQuery(
        IReadOnlyList<AttributeWeightInput> Attributes,
        int Offset = 0,
        int PageSize = DefaultPageSize,
        IReadOnlyList<Guid>? ExcludeIds = null);

    public class ProductCard
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = null!;
        public List<AuthorRef> Authors { get; set; } = [];
        public Guid? PublisherId { get; set; }
        public string? Publisher { get; set; }
        public decimal Price { get; set; }
        public decimal? RatingAverage { get; set; }
        public int RatingCount { get; set; }
        public string? ImageUrl { get; set; }

        public static ProductCard From(StorefrontView v) => new()
        {
            ProductId = v.ProductId,
            Name = v.Name!,
            Authors = v.Authors,
            PublisherId = v.PublisherId,
            Publisher = v.Publisher,
            Price = v.Price!.Value,
            RatingAverage = v.RatingAverage,
            RatingCount = v.RatingCount,
            ImageUrl = v.ImageUrl,
        };
    }

    public class RecommendResponse
    {
        public IReadOnlyList<ProductCard> Cards { get; set; } = [];
    }

    public class GetRecommendedProductsQueryHandler
    {
        public async Task<FeatureObjectResultModel<RecommendResponse>> Handle(
            GetRecommendedProductsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var attributes = (query.Attributes ?? [])
                .Select(a => new RecommendationScoring.AttributeWeight(a.Type, a.Value, a.Weight))
                .ToList();

            if (attributes.Count == 0)
                return FeatureObjectResultModel<RecommendResponse>.Ok(new RecommendResponse());

            var authorNames = attributes
                .Where(a => a.Type == "author").Select(a => a.Value).Distinct().ToList();
            var categories = attributes
                .Where(a => a.Type == "category").Select(a => a.Value).Distinct().ToList();

            // Satılabilir + stoklu + dolu-satır aday tabanı (K7 dolu-satır filtresi).
            var baseQuery = session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null
                            && x.IsAvailableForSale && x.StockQuantity > 0);

            // Aday havuzu: kategori (scalar Contains) + yazar başına jsonb Authors.Any (052 kanıtlı tekil eşleşme).
            var candidates = new Dictionary<Guid, StorefrontView>();

            if (categories.Count > 0)
            {
                var byCategory = await baseQuery
                    .Where(x => categories.Contains(x.Category!))
                    .ToListAsync(ct);
                foreach (var v in byCategory) candidates[v.ProductId] = v;
            }

            foreach (var name in authorNames)
            {
                var byAuthor = await baseQuery
                    .Where(x => x.Authors.Any(a => a.Name == name))
                    .ToListAsync(ct);
                foreach (var v in byAuthor) candidates[v.ProductId] = v;
            }

            var ranked = RecommendationScoring.Rank(candidates.Values, attributes, query.ExcludeIds ?? []);

            // 053 US2: MMR ile kuşak içi arka-arkaya benzeri kır (FR-010); sonra dilimle.
            var diversified = RecommendationScoring.Diversify(ranked, MmrLambda);

            var offset = query.Offset < 0 ? 0 : query.Offset;
            var pageSize = query.PageSize < 1 ? DefaultPageSize : query.PageSize;
            var cards = diversified
                .Skip(offset)
                .Take(pageSize)
                .Select(ProductCard.From)
                .ToList();

            return FeatureObjectResultModel<RecommendResponse>.Ok(new RecommendResponse { Cards = cards });
        }
    }
}

public static class GetRecommendedProductsEndpoint
{
    public static RouteGroupBuilder GetRecommendedProductsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (
                IMessageBus bus,
                GetRecommendedProducts.GetRecommendedProductsQuery request) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetRecommendedProducts.RecommendResponse>>(request);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("GetRecommendedProducts")
            .MapToApiVersion(1, 0)
            .Produces<FeatureObjectResultModel<GetRecommendedProducts.RecommendResponse>>()
            // Vitrin okuması — anonim (login gerektirmez; R12).
            .AllowAnonymous();

        return group;
    }
}
