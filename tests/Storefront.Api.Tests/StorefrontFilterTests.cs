using Storefront.Api.Domains.StorefrontView.Features.Queries;
using static Storefront.Api.Domains.StorefrontView.Features.Queries.GetStorefrontProductList;

namespace Storefront.Api.Tests;

// 016 US1/US2: filtre çekirdeği (Id öncelikli, AND) + facet (Distinct kimlik+ad).
// Kategori event'te zorunlu; null yalnız Catalog'un hiç raporlamadığı kısmi satırda kalır.
public class StorefrontFilterTests
{
    private static readonly Guid ElektronikId = Guid.NewGuid();
    private static readonly Guid GiyimId = Guid.NewGuid();
    private static readonly Guid AppleId = Guid.NewGuid();
    private static readonly Guid NikeId = Guid.NewGuid();

    private static StorefrontView Row(string name, Guid brandId, string brand, Guid categoryId, string category)
    {
        var view = StorefrontView.Create(Guid.NewGuid());
        view.ApplyCatalog(name, "Açıklama", 10m, brandId, brand, categoryId, category, null, isDeleted: false);
        return view;
    }

    private static List<StorefrontView> SampleRows() =>
    [
        Row("Telefon", AppleId, "Apple", ElektronikId, "Elektronik"),
        Row("Laptop", AppleId, "Apple", ElektronikId, "Elektronik"),
        Row("Ayakkabı", NikeId, "Nike", GiyimId, "Giyim"),
        Row("Çanta", NikeId, "Nike", GiyimId, "Giyim")
    ];

    // --- ApplyFilters: kategori ---

    [Fact]
    public void ApplyFilters_ByCategoryId_ReturnsOnlyMatching()
    {
        var result = ApplyFilters(SampleRows().AsQueryable(), ElektronikId, null, null, null).ToList();

        result.Count.ShouldBe(2);
        result.ShouldAllBe(x => x.CategoryId == ElektronikId);
    }

    [Fact]
    public void ApplyFilters_ByCategoryName_WhenIdMissing_MatchesName()
    {
        var result = ApplyFilters(SampleRows().AsQueryable(), null, "Giyim", null, null).ToList();

        result.Count.ShouldBe(2);
        result.ShouldAllBe(x => x.Category == "Giyim");
    }

    [Fact]
    public void ApplyFilters_IdTakesPrecedenceOverName()
    {
        // Id Elektronik'i, ad Giyim'i işaret ediyor — Id kazanmalı.
        var result = ApplyFilters(SampleRows().AsQueryable(), ElektronikId, "Giyim", null, null).ToList();

        result.Count.ShouldBe(2);
        result.ShouldAllBe(x => x.CategoryId == ElektronikId);
    }

    [Fact]
    public void ApplyFilters_NoFilters_ReturnsAll()
    {
        ApplyFilters(SampleRows().AsQueryable(), null, null, null, null).Count().ShouldBe(4);
    }

    [Fact]
    public void ApplyFilters_UnknownCategory_ReturnsEmpty()
    {
        ApplyFilters(SampleRows().AsQueryable(), Guid.NewGuid(), null, null, null).Count().ShouldBe(0);
    }

    // --- ApplyFilters: marka + kombinasyon (US2) ---

    [Fact]
    public void ApplyFilters_ByBrandId_ReturnsOnlyMatching()
    {
        var result = ApplyFilters(SampleRows().AsQueryable(), null, null, NikeId, null).ToList();

        result.Count.ShouldBe(2);
        result.ShouldAllBe(x => x.BrandId == NikeId);
    }

    [Fact]
    public void ApplyFilters_ByBrandName_WhenIdMissing_MatchesName()
    {
        var result = ApplyFilters(SampleRows().AsQueryable(), null, null, null, "Apple").ToList();

        result.Count.ShouldBe(2);
        result.ShouldAllBe(x => x.Brand == "Apple");
    }

    [Fact]
    public void ApplyFilters_CategoryAndBrand_CombineWithAnd()
    {
        var result = ApplyFilters(SampleRows().AsQueryable(), ElektronikId, null, NikeId, null).ToList();

        result.ShouldBeEmpty(); // Elektronik'te Nike ürünü yok
    }

    [Fact]
    public void ApplyFilters_CategoryAndBrand_MatchingCombination()
    {
        var result = ApplyFilters(SampleRows().AsQueryable(), GiyimId, null, NikeId, null).ToList();

        result.Count.ShouldBe(2);
        result.ShouldAllBe(x => x.CategoryId == GiyimId && x.BrandId == NikeId);
    }

    // --- Facet (BuildOptions) ---

    [Fact]
    public void BuildOptions_DistinctIdNamePairs_SortedByName()
    {
        var options = GetStorefrontFilterOptions.BuildOptions(SampleRows());

        options.Categories.Count.ShouldBe(2);
        options.Categories.Select(x => x.Name).ShouldBe(["Elektronik", "Giyim"]);
        options.Categories.Single(x => x.Name == "Elektronik").Id.ShouldBe(ElektronikId);

        options.Brands.Count.ShouldBe(2);
        options.Brands.Select(x => x.Name).ShouldBe(["Apple", "Nike"]);
    }

    [Fact]
    public void BuildOptions_PartialRowsWithoutCatalogData_NotListed()
    {
        // Catalog henüz raporlamadıysa satır kimliksizdir (savunma): facet'e girmez.
        var rows = SampleRows();
        rows.Add(StorefrontView.Create(Guid.NewGuid()));

        var options = GetStorefrontFilterOptions.BuildOptions(rows);

        options.Categories.Count.ShouldBe(2);
        options.Brands.Count.ShouldBe(2);
    }

    [Fact]
    public void BuildOptions_EmptyRows_ReturnsEmptyLists()
    {
        var options = GetStorefrontFilterOptions.BuildOptions([]);

        options.Categories.ShouldBeEmpty();
        options.Brands.ShouldBeEmpty();
    }

    // --- Sayfalama davranışı filtreyle: toplam sayı filtreli sonuca göre (saf normalize zaten testli) ---

    [Fact]
    public void FilteredRows_DriveTotalCount()
    {
        var filtered = ApplyFilters(SampleRows().AsQueryable(), ElektronikId, null, null, null);

        filtered.Count().ShouldBe(2); // sayfa sayısı bu toplamdan türetilir (SC-005)
    }
}