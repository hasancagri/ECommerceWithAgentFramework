using static Storefront.Api.Domains.StorefrontView.RecommendationScoring;

namespace Storefront.Api.Tests;

// 053 US1 (İLKE VI, test-first): ağırlıklı-örtüşme skoru + excludeIds + skor>0 filtresi.
public class RecommendationScoringTests
{
    private static StorefrontView Book(
        string name, string category, string[] authors, Guid? id = null,
        decimal? rating = null, int stock = 5)
    {
        var view = StorefrontView.Create(id ?? Guid.NewGuid());
        view.ApplyCatalog(
            name, "d", 45m,
            authors.Select(a => new AuthorRef(Guid.NewGuid(), a)).ToList(),
            Guid.NewGuid(), "Yayınevi",
            Guid.NewGuid(), category,
            imageUrl: null, isDeleted: false);
        view.ApplyStock(stock);
        if (rating is not null) view.ApplyReviewSummary(rating.Value, 10);
        return view;
    }

    [Fact]
    public void Score_SumsWeightsOfMatchingAttributes()
    {
        var book = Book("Savaş ve Barış", "Tarih", ["Tolstoy"]);
        var attributes = new List<AttributeWeight>
        {
            new("author", "Tolstoy", 0.8m),
            new("category", "Tarih", 0.6m),
            new("author", "Dostoyevski", 0.4m), // eşleşmez
        };

        Score(book, attributes).ShouldBe(1.4m);
    }

    [Fact]
    public void Score_NoOverlap_IsZero()
    {
        var book = Book("Alakasız", "Roman", ["Kemal"]);
        var attributes = new List<AttributeWeight> { new("author", "Tolstoy", 0.8m) };

        Score(book, attributes).ShouldBe(0m);
    }

    [Fact]
    public void Rank_OrdersByScoreDescending_AndDropsZeroScore()
    {
        var strong = Book("Çok eşleşen", "Tarih", ["Tolstoy"]);   // author+category = 1.4
        var weak = Book("Az eşleşen", "Roman", ["Tolstoy"]);      // author = 0.8
        var none = Book("Eşleşmeyen", "Bilim", ["Sagan"]);        // 0 → elenir
        var attributes = new List<AttributeWeight>
        {
            new("author", "Tolstoy", 0.8m),
            new("category", "Tarih", 0.6m),
        };

        var ranked = Rank([weak, none, strong], attributes, []);

        ranked.Select(b => b.ProductId).ShouldBe([strong.ProductId, weak.ProductId]);
    }

    [Fact]
    public void Rank_ExcludesGivenIds()
    {
        var a = Book("A", "Tarih", ["Tolstoy"]);
        var b = Book("B", "Tarih", ["Tolstoy"]);
        var attributes = new List<AttributeWeight> { new("category", "Tarih", 0.6m) };

        var ranked = Rank([a, b], attributes, [a.ProductId]);

        ranked.Select(x => x.ProductId).ShouldBe([b.ProductId]);
    }
}
