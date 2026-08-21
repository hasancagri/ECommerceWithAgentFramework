using Catalog.Api.Domains.SpecificationAttributes;

namespace Catalog.Api.Tests;

// 043 T002: SpecificationAttribute aggregate — seed'li kanonik özellik tanımı (Options child).
public class SpecificationAttributeTests
{
    [Fact]
    public void Create_SetsFieldsAndNormalizedName()
    {
        var result = SpecificationAttribute.Create("Renk", filterable: true, displayOrder: 1);

        result.IsSuccess.ShouldBeTrue();
        var attribute = result.Data!;
        attribute.Name.ShouldBe("Renk");
        attribute.NormalizedName.ShouldNotBeNullOrWhiteSpace();
        attribute.Filterable.ShouldBeTrue();
        attribute.DisplayOrder.ShouldBe(1);
        attribute.Options.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyName_ReturnsError(string name)
    {
        var result = SpecificationAttribute.Create(name, true, 0);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == CatalogResourceConstants.SPEC_NAME_REQUIRED);
    }

    [Fact]
    public void Rename_EmptyName_ReturnsError()
    {
        var attribute = SpecificationAttribute.Create("Renk", true, 0).Data!;

        var result = attribute.Rename(" ");

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == CatalogResourceConstants.SPEC_NAME_REQUIRED);
        attribute.Name.ShouldBe("Renk");
    }

    [Fact]
    public void AddOption_AddsAndReturnsId()
    {
        var attribute = SpecificationAttribute.Create("Renk", true, 0).Data!;

        var result = attribute.AddOption("Siyah", displayOrder: 1);

        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldNotBe(Guid.Empty);
        attribute.Options.Count.ShouldBe(1);
        attribute.Options[0].Name.ShouldBe("Siyah");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AddOption_EmptyName_ReturnsError(string name)
    {
        var attribute = SpecificationAttribute.Create("Renk", true, 0).Data!;

        var result = attribute.AddOption(name, 0);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == CatalogResourceConstants.SPEC_OPTION_NAME_REQUIRED);
    }

    [Fact]
    public void AddOption_DuplicateName_ReturnsError()
    {
        var attribute = SpecificationAttribute.Create("Renk", true, 0).Data!;
        attribute.AddOption("Siyah", 1);

        var result = attribute.AddOption("  siyah ", 2);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == CatalogResourceConstants.SPEC_OPTION_ALREADY_EXISTS);
        attribute.Options.Count.ShouldBe(1);
    }

    [Fact]
    public void SetFilterable_TogglesFlag()
    {
        var attribute = SpecificationAttribute.Create("Renk", true, 0).Data!;

        var result = attribute.SetFilterable(false);

        result.IsSuccess.ShouldBeTrue();
        attribute.Filterable.ShouldBeFalse();
    }
}
