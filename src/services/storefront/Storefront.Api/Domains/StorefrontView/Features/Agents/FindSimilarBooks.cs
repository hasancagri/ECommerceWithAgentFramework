namespace Storefront.Api.Domains.StorefrontView.Features.Agents;

// 067 US2 (FR-004): referans ürünün KENDİ temsiliyle kNN — yeni OpenAI çağrısı YOK. Kendisi hariç,
// yalnız satılabilir + temsili olan adaylar; mesafe eşiği alakasızı eler (SC-005). İki-adım sorgu
// deseni SearchStorefrontProducts'takiyle AYNI ama kendi handler'ında taşınır (bilinçli tekrar —
// agent slice izolasyonu). Referans ürün yok/temsilsiz → Found=false + ReasonCode (hata DEĞİL, SC-002).
public static class FindSimilarBooksForAgent
{
    public const int DefaultMaxResults = 8;
    public const int MaxResultsLimit = 20;

    public record FindSimilarBooksQuery(Guid ProductId, int? MaxResults = null);

    public class FindSimilarBooksResponse
    {
        public bool Found { get; set; }
        // Found=false nedeni (LLM dürüst açıklasın): temsil yok vs gerçekten benzer yok.
        public string? ReasonCode { get; set; }
        public List<SearchStorefrontProductsForAgent.SearchStorefrontProductItem> Items { get; set; } = [];
    }

    public class FindSimilarBooksQueryHandler
    {
        public async Task<FeatureObjectResultModel<FindSimilarBooksResponse>> Handle(
            FindSimilarBooksQuery query,
            IQuerySession session,
            SemanticSearchOption semanticOptions,
            CancellationToken ct)
        {
            var maxResults = query.MaxResults is null
                ? DefaultMaxResults
                : Math.Clamp(query.MaxResults.Value, 1, MaxResultsLimit);

            // Sorgu vektörü = ürünün DB'deki kendi temsili (kontrat #2 — OpenAI çağrısı yok).
            var source = await session.LoadAsync<ProductDescriptionEmbedding>(query.ProductId, ct);
            if (source is null)
                return FeatureObjectResultModel<FindSimilarBooksResponse>.Ok(new FindSimilarBooksResponse
                {
                    Found = false,
                    ReasonCode = StorefrontResourceConstants.STOREFRONT_SIMILARITY_SOURCE_UNAVAILABLE
                });

            // Aday: satılabilir + kendisi hariç (yalnız PK projeksiyonu — satır gövdesi çekilmez).
            var candidateIds = (await session.Query<StorefrontView>()
                    .Where(x => !x.IsDeleted && x.Name != null && x.Price != null
                                && x.ProductId != query.ProductId)
                    .Select(x => x.ProductId)
                    .ToListAsync(ct))
                .ToArray();

            if (candidateIds.Length == 0)
                return FeatureObjectResultModel<FindSimilarBooksResponse>.Ok(
                    new FindSimilarBooksResponse { Found = false });

            // kNN + eşik tek tip-güvenli sorgu yardımcısında (ham SQL'in tek evi: ProductEmbeddingKnnQuery).
            var ordered = await ProductEmbeddingKnnQuery.NearestAsync(
                session, candidateIds, source.Vector, semanticOptions.MaxCosineDistance, maxResults, ct);

            if (ordered.Count == 0)
                return FeatureObjectResultModel<FindSimilarBooksResponse>.Ok(
                    new FindSimilarBooksResponse { Found = false });

            // kNN sırası korunur; satır gövdeleri yalnız dönen N ürün için yüklenir.
            var views = (await session.LoadManyAsync<StorefrontView>(ct, ordered.Select(e => e.ProductId).ToArray()))
                .ToDictionary(v => v.ProductId);

            return FeatureObjectResultModel<FindSimilarBooksResponse>.Ok(new FindSimilarBooksResponse
            {
                Found = true,
                Items = ordered
                    .Where(e => views.ContainsKey(e.ProductId))
                    .Select(e => SearchStorefrontProductsForAgent.SearchStorefrontProductItem.From(views[e.ProductId]))
                    .ToList()
            });
        }
    }
}