using static Storefront.Api.Domains.StorefrontView.Features.Queries.GetPersonalFeed;

namespace Storefront.Api.Tests;

// 054: saf feed sıralayıcısı — İlke VI test-first sözleşmesi. Eşleşme önceliği (yazar > kategori),
// tie-break (puan DESC null-son → ad ASC), satın alınan ürün/aile eleme, aile→tek kart temsilcisi,
// 12 kesme. Marten'siz, bellek-içi.
public class PersonalFeedRankerTests
{
    private static readonly Guid SignalAuthor = Guid.NewGuid();
    private static readonly Guid SignalCategory = Guid.NewGuid();

    private static StorefrontView View(
        string name,
        decimal price = 10m,
        Guid? authorId = null,
        Guid? categoryId = null,
        string? familyCode = null,
        int? stock = 5,
        decimal? rating = null,
        Guid? productId = null)
    {
        var view = StorefrontView.Create(productId ?? Guid.NewGuid());
        view.ApplyCatalog(name, "desc", price,
            [new AuthorRef(authorId ?? Guid.NewGuid(), "Yazar")],
            Guid.NewGuid(), "Yayınevi",
            categoryId ?? Guid.NewGuid(), "Kategori",
            null, false, null, familyCode);
        if (stock.HasValue)
            view.ApplyStock(stock.Value);
        if (rating.HasValue)
            view.ApplyReviewSummary(rating.Value, 1);
        return view;
    }

    private static IReadOnlyList<FeedCard> Rank(
        IEnumerable<StorefrontView> candidates,
        IReadOnlySet<Guid>? purchasedIds = null,
        IReadOnlySet<string>? purchasedFamilies = null) =>
        RankFeed(candidates,
            purchasedIds ?? new HashSet<Guid>(),
            purchasedFamilies ?? new HashSet<string>(),
            new HashSet<Guid> { SignalAuthor },
            new HashSet<Guid> { SignalCategory });

    [Fact]
    public void AuthorMatch_RanksBeforeCategoryMatch()
    {
        var byCategory = View("Aaa Kategori Kitabı", categoryId: SignalCategory, rating: 5.0m);
        var byAuthor = View("Zzz Yazar Kitabı", authorId: SignalAuthor);

        var feed = Rank([byCategory, byAuthor]);

        feed.Count.ShouldBe(2);
        feed[0].Representative.ProductId.ShouldBe(byAuthor.ProductId);
        feed[0].MatchType.ShouldBe(MatchTypeAuthor);
        feed[1].MatchType.ShouldBe(MatchTypeCategory);
    }

    [Fact]
    public void SameMatchType_TieBreak_RatingDescNullLast_ThenNameAsc()
    {
        var ratedB = View("B Kitabı", categoryId: SignalCategory, rating: 4.0m);
        var unratedA = View("A Kitabı", categoryId: SignalCategory);
        var ratedA = View("A Kitabı", categoryId: SignalCategory, rating: 4.0m);

        var feed = Rank([ratedB, unratedA, ratedA]);

        feed.Select(c => c.Representative.ProductId).ShouldBe(
            [ratedA.ProductId, ratedB.ProductId, unratedA.ProductId]);
    }

    [Fact]
    public void PurchasedProduct_IsExcluded()
    {
        var owned = View("Alınmış", authorId: SignalAuthor);
        var fresh = View("Alınmamış", authorId: SignalAuthor);

        var feed = Rank([owned, fresh], purchasedIds: new HashSet<Guid> { owned.ProductId });

        feed.ShouldHaveSingleItem().Representative.ProductId.ShouldBe(fresh.ProductId);
    }

    [Fact]
    public void PurchasedFamily_OtherVariants_AreExcluded()
    {
        var sibling = View("Aynı Ailenin Diğer Boyu", authorId: SignalAuthor, familyCode: "fam-1");
        var unrelated = View("Bağımsız", authorId: SignalAuthor);

        var feed = Rank([sibling, unrelated], purchasedFamilies: new HashSet<string> { "fam-1" });

        feed.ShouldHaveSingleItem().Representative.ProductId.ShouldBe(unrelated.ProductId);
    }

    [Fact]
    public void CandidateMatchingNeitherSignal_IsDropped()
    {
        var noise = View("Alakasız");

        Rank([noise]).ShouldBeEmpty();
    }

    [Fact]
    public void Family_CollapsesToSingleCard_InStockRepresentative_WithVariantCount()
    {
        var cheapOutOfStock = View("Varyant Ucuz", price: 5m, authorId: SignalAuthor,
            familyCode: "fam-2", stock: 0);
        var costlyInStock = View("Varyant Stoklu", price: 15m, authorId: SignalAuthor,
            familyCode: "fam-2", stock: 3);

        var feed = Rank([cheapOutOfStock, costlyInStock]);

        var card = feed.ShouldHaveSingleItem();
        card.Representative.ProductId.ShouldBe(costlyInStock.ProductId);
        card.VariantCount.ShouldBe(2);
    }

    [Fact]
    public void MatchingBothSignals_CountsAsAuthorMatch()
    {
        var both = View("İkisi Birden", authorId: SignalAuthor, categoryId: SignalCategory);

        var feed = Rank([both]);

        feed.ShouldHaveSingleItem().MatchType.ShouldBe(MatchTypeAuthor);
    }

    [Fact]
    public void Feed_IsCappedAtTwelveCards()
    {
        var candidates = Enumerable.Range(0, 15)
            .Select(i => View($"Kitap {i:D2}", categoryId: SignalCategory))
            .ToList();

        Rank(candidates).Count.ShouldBe(FeedSize);
        FeedSize.ShouldBe(12);
    }

    [Fact]
    public void EmptyCandidates_YieldEmptyFeed()
    {
        Rank([]).ShouldBeEmpty();
    }
}