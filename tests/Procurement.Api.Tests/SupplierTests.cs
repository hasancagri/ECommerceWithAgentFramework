using Procurement.Api.Constants;
using Procurement.Api.Domains.Suppliers;
using Procurement.Api.Domains.Suppliers.ValueObjects;
using Shouldly;
using Xunit;

namespace Procurement.Api.Tests;

// Supplier aggregate saf domain testleri (İlke VI — test-first).
public class SupplierTests
{
    private static Supplier CreateSupplier()
        => Supplier.Create("supplier-a", "Tedarikçi A", 1).Data!;

    [Fact]
    public void Create_ValidInput_Succeeds()
    {
        var result = Supplier.Create("supplier-a", "Tedarikçi A", 1);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Code.ShouldBe("supplier-a");
        result.Data.Name.ShouldBe("Tedarikçi A");
        result.Data.Priority.ShouldBe(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyCode_Fails(string code)
    {
        var result = Supplier.Create(code, "Tedarikçi A", 1);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.SUPPLIER_CODE_REQUIRED);
    }

    [Fact]
    public void Create_EmptyName_Fails()
    {
        var result = Supplier.Create("supplier-a", " ", 1);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.SUPPLIER_NAME_REQUIRED);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Create_NonPositivePriority_Fails(int priority)
    {
        var result = Supplier.Create("supplier-a", "Tedarikçi A", priority);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.SUPPLIER_PRIORITY_INVALID);
    }

    [Fact]
    public void ResolveCategory_KnownName_ReturnsMapping()
    {
        var supplier = CreateSupplier();
        supplier.SetCategoryMappings(
        [
            CategoryMapping.Create("Elektronik/Telefon", "Elektronik", "Telefon"),
        ]).IsSuccess.ShouldBeTrue();

        var result = supplier.ResolveCategory("Elektronik/Telefon");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.CanonicalCategory.ShouldBe("Elektronik");
        result.Data.CanonicalSubCategory.ShouldBe("Telefon");
    }

    [Fact]
    public void ResolveCategory_IsCaseAndWhitespaceInsensitive()
    {
        var supplier = CreateSupplier();
        supplier.SetCategoryMappings(
        [
            CategoryMapping.Create("Phones", "Elektronik", "Telefon"),
        ]);

        var result = supplier.ResolveCategory("  phones ");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.CanonicalCategory.ShouldBe("Elektronik");
    }

    [Fact]
    public void ResolveCategory_UnknownName_Fails()
    {
        var supplier = CreateSupplier();
        supplier.SetCategoryMappings(
        [
            CategoryMapping.Create("Phones", "Elektronik", "Telefon"),
        ]);

        var result = supplier.ResolveCategory("Bilinmeyen Kategori");

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.CATEGORY_MAPPING_NOT_FOUND);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ResolveCategory_MissingRawName_Fails(string? rawName)
    {
        var supplier = CreateSupplier();

        var result = supplier.ResolveCategory(rawName);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.CATEGORY_MAPPING_NOT_FOUND);
    }
}