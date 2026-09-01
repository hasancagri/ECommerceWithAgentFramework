using UserPurchaseDoc = Storefront.Api.Domains.UserPurchase.UserPurchase;

namespace Storefront.Api.Domains.StorefrontView.Features.Queries;

// 054: kişisel ana sayfa feed'i — kullanıcının satın-alma geçmişinden (UserPurchase) kategori+yazar
// sinyali çıkarır, o kümeden HENÜZ ALINMAMIŞ kitapları döner. Heuristik/.NET-içi; motor yok.
// Cache YOK (kullanıcıya özel yanıt — herkese-aynı şartı sağlamaz).
public static class GetPersonalFeed
{
    public const int FeedSize = 12;
    public const string MatchTypeAuthor = "Author";
    public const string MatchTypeCategory = "Category";

    public record GetPersonalFeedQuery(Guid UserId);

    // Saf sıralayıcının kart çıktısı: aile temsilcisi + görünür üye adedi + eşleşme türü.
    public record FeedCard(StorefrontView Representative, int VariantCount, string MatchType);

    // Saf, test edilebilir feed çekirdeği (İlke VI test-first sözleşmesi — PersonalFeedRankerTests):
    // satın alınan ürün + ailesi elenir; yazar eşleşmesi kategori eşleşmesinden önce; tie →
    // RatingAverage DESC (null son) → Name ASC; aile → tek kart (stok>0 önce, ucuz önce, ProductId
    // ASC — 045 temsilci kuralının bilinçli kopyası, slice kendi kuralını taşır); ilk 12 kart.
    public static IReadOnlyList<FeedCard> RankFeed(
        IEnumerable<StorefrontView> candidates,
        IReadOnlySet<Guid> purchasedProductIds,
        IReadOnlySet<string> purchasedFamilyCodes,
        IReadOnlySet<Guid> signalAuthorIds,
        IReadOnlySet<Guid> signalCategoryIds)
    {
        var eligible = candidates
            .Where(v => !purchasedProductIds.Contains(v.ProductId))
            .Where(v => v.FamilyCode is null || !purchasedFamilyCodes.Contains(v.FamilyCode))
            .Select(v => (View: v, MatchType: MatchTypeFor(v, signalAuthorIds, signalCategoryIds)))
            .Where(x => x.MatchType is not null)
            .ToList();

        return eligible
            .GroupBy(x => string.IsNullOrWhiteSpace(x.View.FamilyCode)
                ? x.View.ProductId.ToString()
                : x.View.FamilyCode!)
            .Select(g => new FeedCard(
                g.Select(x => x.View)
                    .OrderByDescending(m => m.StockQuantity is > 0)
                    .ThenBy(m => m.Price ?? decimal.MaxValue)
                    .ThenBy(m => m.ProductId)
                    .First(),
                g.Count(),
                g.Any(x => x.MatchType == MatchTypeAuthor) ? MatchTypeAuthor : MatchTypeCategory))
            .OrderBy(c => c.MatchType == MatchTypeAuthor ? 0 : 1)
            .ThenByDescending(c => c.Representative.RatingAverage ?? decimal.MinValue)
            .ThenBy(c => c.Representative.Name)
            .Take(FeedSize)
            .ToList();
    }

    // Yazar sinyali kategoriden güçlü sayılır (FR-009); iki sinyale de uymayan aday feed'e girmez.
    private static string? MatchTypeFor(
        StorefrontView v, IReadOnlySet<Guid> signalAuthorIds, IReadOnlySet<Guid> signalCategoryIds)
    {
        if (v.Authors.Any(a => signalAuthorIds.Contains(a.Id)))
            return MatchTypeAuthor;
        if (v.CategoryId is not null && signalCategoryIds.Contains(v.CategoryId.Value))
            return MatchTypeCategory;
        return null;
    }

    // Kart gövdesi liste yanıtıyla (StorefrontProductResponse) aynı alanları taşır — WebApp aynı
    // ürün kartını çizer; ek MatchType feed'e özgü (hangi sinyalle geldi).
    public class PersonalFeedItemResponse
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public List<AuthorRef> Authors { get; set; } = [];
        public Guid? PublisherId { get; set; }
        public string? Publisher { get; set; }
        public Guid? CategoryId { get; set; }
        public string? Category { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public int? StockQuantity { get; set; }
        public bool? IsInStock { get; set; }
        public decimal? RatingAverage { get; set; }
        public int RatingCount { get; set; }
        public int VariantCount { get; set; }
        public string MatchType { get; set; } = null!;

        public static PersonalFeedItemResponse From(FeedCard card) => new()
        {
            ProductId = card.Representative.ProductId,
            Name = card.Representative.Name!,
            Description = card.Representative.Description ?? string.Empty,
            Authors = card.Representative.Authors,
            PublisherId = card.Representative.PublisherId,
            Publisher = card.Representative.Publisher,
            CategoryId = card.Representative.CategoryId,
            Category = card.Representative.Category,
            Price = card.Representative.Price!.Value,
            ImageUrl = card.Representative.ImageUrl,
            StockQuantity = card.Representative.StockQuantity,
            IsInStock = card.Representative.StockQuantity.HasValue ? card.Representative.StockQuantity > 0 : null,
            RatingAverage = card.Representative.RatingAverage,
            RatingCount = card.Representative.RatingCount,
            VariantCount = card.VariantCount,
            MatchType = card.MatchType
        };
    }

    public class GetPersonalFeedQueryHandler
    {
        public async Task<FeatureListResultModel<PersonalFeedItemResponse>> Handle(
            GetPersonalFeedQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var purchases = await session.Query<UserPurchaseDoc>()
                .Where(x => x.UserId == query.UserId)
                .ToListAsync(ct);

            // Sinyalsiz kullanıcı: 200 + boş liste (hata DEĞİL) — WebApp boş durumu çizer (FR-006).
            if (purchases.Count == 0)
                return FeatureListResultModel<PersonalFeedItemResponse>.Ok([]);

            var purchasedIds = purchases.Select(p => p.ProductId).ToHashSet();

            // Sinyal kümeleri satın alma ANI verisinden değil, kendi güncel satırlarımızdan (R2).
            var ownedViews = await session.LoadManyAsync<StorefrontView>(ct, purchasedIds.ToArray());
            var signalCategoryIds = ownedViews
                .Where(v => v.CategoryId is not null)
                .Select(v => v.CategoryId!.Value)
                .ToHashSet();
            var signalAuthorIds = ownedViews
                .SelectMany(v => v.Authors)
                .Select(a => a.Id)
                .ToHashSet();
            var purchasedFamilies = ownedViews
                .Where(v => !string.IsNullOrWhiteSpace(v.FamilyCode))
                .Select(v => v.FamilyCode!)
                .ToHashSet();

            // Adaylar DB'den iki kanıtlı sorgu deseniyle çekilir (040/043 dersleri — riskli jsonb OR
            // kompozisyonu yerine): kategori IsOneOf + yazar başına Authors.Any (052'de canlı doğrulanan
            // çeviri). Sinyal kümeleri küçük (kullanıcının aldığı kitapların yazarları) — N küçük sorgu.
            var sellable = session.Query<StorefrontView>()
                .Where(x => !x.IsDeleted && x.Name != null && x.Price != null);

            var candidates = new Dictionary<Guid, StorefrontView>();

            if (signalCategoryIds.Count > 0)
            {
                var categoryArray = signalCategoryIds.Select(id => (Guid?)id).ToArray();
                var byCategory = await sellable
                    .Where(x => x.CategoryId.IsOneOf(categoryArray))
                    .ToListAsync(ct);
                foreach (var v in byCategory)
                    candidates[v.ProductId] = v;
            }

            foreach (var authorId in signalAuthorIds)
            {
                var byAuthor = await sellable
                    .Where(x => x.Authors.Any(a => a.Id == authorId))
                    .ToListAsync(ct);
                foreach (var v in byAuthor)
                    candidates[v.ProductId] = v;
            }

            var cards = RankFeed(candidates.Values,
                purchasedIds, purchasedFamilies, signalAuthorIds, signalCategoryIds);

            return FeatureListResultModel<PersonalFeedItemResponse>.Ok(
                cards.Select(PersonalFeedItemResponse.From).ToList());
        }
    }
}

public static class GetPersonalFeedEndpoint
{
    public static RouteGroupBuilder GetPersonalFeedGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/personal-feed", async (HttpContext httpContext, ICurrentUser currentUser, IMessageBus bus) =>
            {
                // Kimlik token'dan çözülür; userId parametreyle ALINMAZ (kontrat).
                var userId = currentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureListResultModel<GetPersonalFeed.PersonalFeedItemResponse>>(
                    new GetPersonalFeed.GetPersonalFeedQuery(userId));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("GetPersonalFeed")
            .MapToApiVersion(1, 0)
            .Produces<FeatureListResultModel<GetPersonalFeed.PersonalFeedItemResponse>>()
            .RequireAuthorization(AuthorizationScopes.StorefrontRead);

        return group;
    }
}