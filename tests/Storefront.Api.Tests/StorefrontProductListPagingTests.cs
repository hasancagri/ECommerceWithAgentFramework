using static Storefront.Api.Domains.StorefrontView.Features.Queries.GetStorefrontProductList;

namespace Storefront.Api.Tests;

// 011: sayfa normalizasyonu (FR-005) ve varsayılanlar — saf birim testleri.
public class StorefrontProductListPagingTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void NormalizePageNumber_ValuesBelowOne_NormalizeToFirstPage(int input, int expected)
    {
        GetStorefrontProductListQuery.NormalizePageNumber(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(-1, 12)]
    [InlineData(8, 8)]
    [InlineData(12, 12)]
    public void NormalizePageSize_ValuesBelowOne_FallBackToDefault(int input, int expected)
    {
        GetStorefrontProductListQuery.NormalizePageSize(input).ShouldBe(expected);
    }

    [Fact]
    public void Query_Defaults_FirstPageWithTwelveItems()
    {
        var query = new GetStorefrontProductListQuery();

        query.PageNumber.ShouldBe(1);
        query.PageSize.ShouldBe(12);
    }
}