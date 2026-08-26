using static Storefront.Api.Domains.StorefrontView.Features.Agents.SearchStorefrontProductsForAgent;

namespace Storefront.Api.Tests;

// 019 US1/US3: hibrit arama query'sinin saf çekirdeği — doğrulama, MaxResults kırpma,
// filtre birleşimi (marka OR case-insensitive, fiyat aralığı, asgari stok, Name ASC).
public class SearchStorefrontProductsTests
{
    private static StorefrontView Row(string name, string brand, decimal price, int? stock)
    {
        var view = StorefrontView.Create(Guid.NewGuid());
        view.ApplyCatalog(name, "Açıklama", price,
            Guid.NewGuid(), brand, Guid.NewGuid(), "Kategori", null, isDeleted: false);
        if (stock is not null)
            view.ApplyStock(stock.Value);
        return view;
    }

    private static List<StorefrontView> SampleRows() =>
    [
        Row("Ayakkabı", "Nike", 2500m, 5),
        Row("Bot", "Adidas", 1500m, 1),
        Row("Çanta", "Nike", 5000m, 0),
        Row("Telefon", "Apple", 30000m, null)
    ];

    // --- Doğrulama (FR-003, edge cases) ---

    [Fact]
    public void Validate_NoCriteria_ReturnsRequiredError()
    {
        var messages = Validate(new SearchStorefrontProductsQuery());

        messages.ShouldHaveSingleItem().Code.ShouldBe(StorefrontResourceConstants.VALUE_IS_REQUIRED);
    }

    [Fact]
    public void Validate_OnlyMaxResults_IsNotACriteria()
    {
        var messages = Validate(new SearchStorefrontProductsQuery(MaxResults: 5));

        messages.ShouldHaveSingleItem().Code.ShouldBe(StorefrontResourceConstants.VALUE_IS_REQUIRED);
    }

    [Fact]
    public void Validate_MinPriceGreaterThanMaxPrice_ReturnsRangeError()
    {
        var messages = Validate(new SearchStorefrontProductsQuery(MinPrice: 3000m, MaxPrice: 1000m));

        messages.ShouldHaveSingleItem().Code.ShouldBe(StorefrontResourceConstants.INVALID_RANGE);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, -5)]
    public void Validate_NegativePrice_ReturnsInvalidValue(int? minPrice, int? maxPrice)
    {
        var query = new SearchStorefrontProductsQuery(MinPrice: minPrice, MaxPrice: maxPrice);

        Validate(query).ShouldHaveSingleItem().Code.ShouldBe(StorefrontResourceConstants.INVALID_VALUE);
    }

    [Fact]
    public void Validate_MinStockBelowOne_ReturnsInvalidValue()
    {
        var messages = Validate(new SearchStorefrontProductsQuery(MinStock: 0));

        messages.ShouldHaveSingleItem().Code.ShouldBe(StorefrontResourceConstants.INVALID_VALUE);
    }


    // --- MaxResults kırpma (FR-009) ---

    [Theory]
    [InlineData(null, 8)]   // varsayılan
    [InlineData(0, 1)]      // alt sınır
    [InlineData(-3, 1)]
    [InlineData(12, 12)]
    [InlineData(50, 20)]    // üst sınır
    public void NormalizeMaxResults_ClampsToRange(int? requested, int expected)
    {
        NormalizeMaxResults(requested).ShouldBe(expected);
    }

    // --- Filtre birleşimi (FR-004/005/008) ---

    [Fact]
    public void FilterAndOrder_BrandsAreOrCombinedCaseInsensitive()
    {
        var query = new SearchStorefrontProductsQuery(Brands: ["nike", "ADIDAS"]);

        var result = FilterAndOrder(SampleRows(), query);

        result.Count.ShouldBe(3);
        result.ShouldAllBe(x => x.Brand == "Nike" || x.Brand == "Adidas");
    }

    [Fact]
    public void FilterAndOrder_UnknownBrand_MatchesNothing()
    {
        var result = FilterAndOrder(SampleRows(), new SearchStorefrontProductsQuery(Brands: ["Puma"]));

        result.ShouldBeEmpty();
    }

    [Fact]
    public void FilterAndOrder_PriceRange_IsInclusive()
    {
        var query = new SearchStorefrontProductsQuery(MinPrice: 1500m, MaxPrice: 2500m);

        var result = FilterAndOrder(SampleRows(), query);

        result.Select(x => x.Name).ShouldBe(["Ayakkabı", "Bot"]); // Name ASC
    }

    [Fact]
    public void FilterAndOrder_MinStock_ExcludesBelowAndUnknown()
    {
        var query = new SearchStorefrontProductsQuery(MinStock: 2);

        var result = FilterAndOrder(SampleRows(), query);

        // stok 1 (Bot), 0 (Çanta) ve bilinmeyen (Telefon) elenir (US1-S2).
        result.ShouldHaveSingleItem().Name.ShouldBe("Ayakkabı");
    }

    [Fact]
    public void FilterAndOrder_CombinedFilters_AreAnded()
    {
        var query = new SearchStorefrontProductsQuery(Brands: ["Nike"], MaxPrice: 3000m, MinStock: 1);

        var result = FilterAndOrder(SampleRows(), query);

        result.ShouldHaveSingleItem().Name.ShouldBe("Ayakkabı");
    }

    [Fact]
    public void FilterAndOrder_RespectsMaxResults()
    {
        var query = new SearchStorefrontProductsQuery(MinPrice: 0m, MaxResults: 2);

        var result = FilterAndOrder(SampleRows(), query);

        result.Count.ShouldBe(2);
        result.Select(x => x.Name).ShouldBe(["Ayakkabı", "Bot"]); // deterministik Name ASC
    }
}