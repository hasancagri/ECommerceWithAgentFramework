using Personalization.Api.Constants;
using Personalization.Api.Domains.PurchaseSignals.ValueObjects;
using Shouldly;
using Xunit;

namespace Personalization.Api.Tests;

// 048 US1 — kalem invariant'lari (adet>0, tutar>=0, gecerli referans). VO kendi invariant'ini korur.
public class PurchaseSignalItemTests
{
    private static readonly Guid ProductId = Guid.Parse("9c8d0000-0000-0000-0000-000000000003");

    [Fact]
    public void Create_ValidItem_Succeeds()
    {
        var result = PurchaseSignalItem.Create(ProductId, "Electronics", "Acme", 2, 199.90m);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.ProductId.ShouldBe(ProductId);
        result.Data.Quantity.ShouldBe(2);
        result.Data.UnitPrice.ShouldBe(199.90m);
        result.Data.Category.ShouldBe("Electronics");
        result.Data.Brand.ShouldBe("Acme");
    }

    [Fact]
    public void Create_NullCategoryAndBrand_Succeeds()
    {
        // D3: Order kategori/marka tutmuyorsa null gelir; kalem yine gecerli.
        var result = PurchaseSignalItem.Create(ProductId, null, null, 1, 0m);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Category.ShouldBeNull();
        result.Data.Brand.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveQuantity_Fails(int quantity)
    {
        var result = PurchaseSignalItem.Create(ProductId, null, null, quantity, 10m);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == PersonalizationResourceConstants.PURCHASE_SIGNAL_QUANTITY_INVALID);
    }

    [Fact]
    public void Create_NegativeUnitPrice_Fails()
    {
        var result = PurchaseSignalItem.Create(ProductId, null, null, 1, -0.01m);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == PersonalizationResourceConstants.PURCHASE_SIGNAL_UNIT_PRICE_INVALID);
    }

    [Fact]
    public void Create_EmptyProductId_Fails()
    {
        var result = PurchaseSignalItem.Create(Guid.Empty, null, null, 1, 10m);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == PersonalizationResourceConstants.PURCHASE_SIGNAL_REFERENCE_INVALID);
    }
}