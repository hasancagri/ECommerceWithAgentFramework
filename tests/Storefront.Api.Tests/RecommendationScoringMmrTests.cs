using static Storefront.Api.Domains.StorefrontView.RecommendationScoring;

namespace Storefront.Api.Tests;

// 053 US2 (İLKE VI, test-first): MMR (λ) arka-arkaya birebir benzeri kırar (FR-010).
public class RecommendationScoringMmrTests
{
    private static StorefrontView Book(string name, string category, string[] authors)
    {
        var view = StorefrontView.Create(Guid.NewGuid());
        view.ApplyCatalog(
            name, "d", 45m,
            authors.Select(a => new AuthorRef(Guid.NewGuid(), a)).ToList(),
            Guid.NewGuid(), "Yayınevi", Guid.NewGuid(), category,
            imageUrl: null, isDeleted: false);
        view.ApplyStock(5);
        return view;
    }

    [Fact]
    public void Diversify_BreaksAdjacentNearIdentical()
    {
        // İki birebir benzer (Tolstoy/Tarih) yüksek skorlu + bir farklı (Kemal/Roman) düşük skorlu.
        var a1 = Book("Tolstoy-1", "Tarih", ["Tolstoy"]);
        var a2 = Book("Tolstoy-2", "Tarih", ["Tolstoy"]);
        var b = Book("Kemal", "Roman", ["Kemal"]);
        var attributes = new List<AttributeWeight>
        {
            new("author", "Tolstoy", 1.0m),
            new("category", "Tarih", 0.5m),
            new("author", "Kemal", 0.9m),
            new("category", "Roman", 0.1m),
        };

        // Salt skor sırası: a1, a2 (1.5), sonra b (1.0) — iki benzer yan yana.
        var ranked = Rank([a1, a2, b], attributes, []);
        ranked[0].Category.ShouldBe("Tarih");
        ranked[1].Category.ShouldBe("Tarih");

        // MMR: farklı olan (b) araya girer → iki Tolstoy artık yan yana değil.
        var diversified = Diversify(ranked, 0.5m);
        var categories = diversified.Select(x => x.Category).ToList();
        var firstTarih = categories.IndexOf("Tarih");
        var lastTarih = categories.LastIndexOf("Tarih");
        (lastTarih - firstTarih).ShouldBeGreaterThan(1); // aralarında en az bir farklı öğe
    }

    [Fact]
    public void Diversify_PreservesSetAndCount()
    {
        var a = Book("A", "Tarih", ["Tolstoy"]);
        var b = Book("B", "Roman", ["Kemal"]);
        var attributes = new List<AttributeWeight> { new("category", "Tarih", 1m), new("category", "Roman", 1m) };
        var ranked = Rank([a, b], attributes, []);

        var diversified = Diversify(ranked, 0.5m);

        diversified.Count.ShouldBe(2);
        diversified.Select(x => x.ProductId).OrderBy(x => x).ShouldBe(
            new[] { a.ProductId, b.ProductId }.OrderBy(x => x));
    }
}
