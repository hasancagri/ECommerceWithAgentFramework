namespace Storefront.Api.Tests;

// 043 T010: spec filtresi çekirdeği — grup içi OR, gruplar arası AND (FR-008) + facet sayımı (SC-006).
public class StorefrontSpecFilterTests
{
    private static StorefrontView Row(params (string Attribute, string Option)[] specs)
    {
        var view = StorefrontView.Create(Guid.NewGuid());
        view.ApplyCatalog("Ürün", "açıklama", 10m, Guid.NewGuid(), "Marka", Guid.NewGuid(), "Kategori",
            null, isDeleted: false,
            specs.Select(s => SpecPair.Create(s.Attribute, s.Option)).ToList());
        return view;
    }

    // --- SpecKeys türetimi ---

    [Fact]
    public void ApplyCatalog_DerivesSpecKeys()
    {
        var view = Row(("Renk", "Siyah"), ("Materyal", "Çelik"));

        view.Specs.Count.ShouldBe(2);
        view.SpecKeys.ShouldBe(new[] { "Renk|Siyah", "Materyal|Çelik" });
    }

    [Fact]
    public void ApplyCatalog_NoSpecs_EmptyKeys()
    {
        var view = Row();

        view.Specs.ShouldBeEmpty();
        view.SpecKeys.ShouldBeEmpty();
    }

    // --- ParseSpecGroups ---

    [Fact]
    public void ParseSpecGroups_GroupsByAttribute()
    {
        var groups = GetStorefrontProductList.ParseSpecGroups(
            ["Renk|Siyah", "Renk|Beyaz", "Materyal|Çelik"]);

        groups.Count.ShouldBe(2);
        groups["Renk"].ShouldBe(new[] { "Renk|Siyah", "Renk|Beyaz" });
        groups["Materyal"].ShouldBe(new[] { "Materyal|Çelik" });
    }

    [Fact]
    public void ParseSpecGroups_IgnoresInvalidEntries()
    {
        var groups = GetStorefrontProductList.ParseSpecGroups(
            ["bozuk", "", "  ", "A|B|C", "Renk|Siyah"]);

        groups.Count.ShouldBe(1);
        groups["Renk"].ShouldBe(new[] { "Renk|Siyah" });
    }

    // --- MatchesSpecFilters (bellek-içi semantik çekirdeği) ---

    [Fact]
    public void Matches_EmptyFilter_MatchesAll()
    {
        var groups = GetStorefrontProductList.ParseSpecGroups([]);

        GetStorefrontProductList.MatchesSpecFilters(Row(), groups).ShouldBeTrue();
    }

    [Fact]
    public void Matches_SameAttribute_IsOr()
    {
        var groups = GetStorefrontProductList.ParseSpecGroups(["Renk|Siyah", "Renk|Beyaz"]);

        GetStorefrontProductList.MatchesSpecFilters(Row(("Renk", "Siyah")), groups).ShouldBeTrue();
        GetStorefrontProductList.MatchesSpecFilters(Row(("Renk", "Beyaz")), groups).ShouldBeTrue();
        GetStorefrontProductList.MatchesSpecFilters(Row(("Renk", "Gri")), groups).ShouldBeFalse();
    }

    [Fact]
    public void Matches_DifferentAttributes_IsAnd()
    {
        var groups = GetStorefrontProductList.ParseSpecGroups(["Renk|Siyah", "Materyal|Çelik"]);

        GetStorefrontProductList.MatchesSpecFilters(
            Row(("Renk", "Siyah"), ("Materyal", "Çelik")), groups).ShouldBeTrue();
        GetStorefrontProductList.MatchesSpecFilters(Row(("Renk", "Siyah")), groups).ShouldBeFalse();
        GetStorefrontProductList.MatchesSpecFilters(Row(("Materyal", "Çelik")), groups).ShouldBeFalse();
    }

    [Fact]
    public void Matches_SpeclessRow_FailsAnySpecFilter()
    {
        var groups = GetStorefrontProductList.ParseSpecGroups(["Renk|Siyah"]);

        GetStorefrontProductList.MatchesSpecFilters(Row(), groups).ShouldBeFalse();
    }

    // --- Facet (BuildOptions specifications bölümü) ---

    [Fact]
    public void BuildOptions_CountsPerOption()
    {
        var rows = new[]
        {
            Row(("Renk", "Siyah")),
            Row(("Renk", "Siyah"), ("Materyal", "Çelik")),
            Row(("Renk", "Beyaz")),
            Row(), // spec'siz satır facet'e girmez
        };

        var options = GetStorefrontFilterOptions.BuildOptions(rows);

        options.Specifications.Count.ShouldBe(2);
        var renk = options.Specifications.Single(s => s.Name == "Renk");
        renk.Options.Single(o => o.Name == "Siyah").Count.ShouldBe(2);
        renk.Options.Single(o => o.Name == "Beyaz").Count.ShouldBe(1);
        options.Specifications.Single(s => s.Name == "Materyal")
            .Options.Single(o => o.Name == "Çelik").Count.ShouldBe(1);
    }

    [Fact]
    public void BuildOptions_NoSpecData_EmptySection()
    {
        var options = GetStorefrontFilterOptions.BuildOptions([Row(), Row()]);

        options.Specifications.ShouldBeEmpty();
    }
}
