using Shouldly;
using Ucp.Api.Domains.Sessions.ValueObjects;
using Xunit;

namespace Ucp.Api.Tests;

// 072 İlke VI: session VO'larının Create + guard'ları test-first (tutarlar minor units).
public class UcpSessionValueObjectsTests
{
    [Fact]
    public void LineItem_Valid_ComputesLineTotal()
    {
        var r = UcpLineItem.Create("p1", "Kitap", 2500, 3);

        r.IsSuccess.ShouldBeTrue();
        r.Data!.LineTotalMinor.ShouldBe(7500);
    }

    [Theory]
    [InlineData("", "Kitap", 100, 1)]   // ürün boş
    [InlineData("p1", "", 100, 1)]       // başlık boş
    [InlineData("p1", "Kitap", -1, 1)]   // negatif fiyat
    [InlineData("p1", "Kitap", 100, 0)]  // adet < 1
    public void LineItem_Invalid_ReturnsError(string productId, string title, long price, int qty)
    {
        UcpLineItem.Create(productId, title, price, qty).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Totals_Compute_GrandIsSubtotalMinusDiscountPlusShipping()
    {
        var t = UcpTotals.Compute(subtotalMinor: 10000, discountTotalMinor: 2000, shippingTotalMinor: 3000);
        t.GrandTotalMinor.ShouldBe(11000);
    }

    [Fact]
    public void Totals_Compute_ClampsNegativeGrandToZero()
    {
        var t = UcpTotals.Compute(subtotalMinor: 1000, discountTotalMinor: 5000, shippingTotalMinor: 0);
        t.GrandTotalMinor.ShouldBe(0);
    }

    [Fact]
    public void Buyer_Valid_Ok()
        => UcpBuyer.Create("a@b.com", "Ada", "Lovelace").IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData("", "Ada", "Lovelace")]
    [InlineData("a@b.com", "", "Lovelace")]
    [InlineData("a@b.com", "Ada", "")]
    public void Buyer_Invalid_ReturnsError(string email, string first, string last)
        => UcpBuyer.Create(email, first, last).IsSuccess.ShouldBeFalse();

    [Fact]
    public void Fulfillment_Valid_Ok()
    {
        var r = UcpFulfillment.Create("shipping", "standard", "Standart", 2999, "dest1");
        r.IsSuccess.ShouldBeTrue();
        r.Data!.CostMinor.ShouldBe(2999);
    }

    [Theory]
    [InlineData("shipping", "", 0)]      // seçenek boş
    [InlineData("", "standard", 0)]       // tip boş
    [InlineData("shipping", "standard", -5)] // negatif bedel
    public void Fulfillment_Invalid_ReturnsError(string method, string optionId, long cost)
        => UcpFulfillment.Create(method, optionId, "l", cost, "d").IsSuccess.ShouldBeFalse();

    [Fact]
    public void AppliedDiscount_Valid_Ok()
        => UcpAppliedDiscount.Create("Kod", 500, "BOOK10").IsSuccess.ShouldBeTrue();

    [Theory]
    [InlineData("", 500)]   // başlık boş
    [InlineData("Kod", 0)]  // tutar <= 0
    [InlineData("Kod", -5)]
    public void AppliedDiscount_Invalid_ReturnsError(string title, long amount)
        => UcpAppliedDiscount.Create(title, amount, null).IsSuccess.ShouldBeFalse();

    [Fact]
    public void Link_Invalid_ReturnsError()
        => UcpLink.Create("", "").IsSuccess.ShouldBeFalse();
}