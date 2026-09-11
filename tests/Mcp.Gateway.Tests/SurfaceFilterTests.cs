using Mcp.Gateway.Options;
using Mcp.Gateway.Routing;
using Shouldly;
using Xunit;

namespace Mcp.Gateway.Tests;

// 073 İlke VI: yüzey süzme (saf) test-first — müşteri/admin çapraz sızıntı yok.
public class SurfaceFilterTests
{
    private static readonly DownstreamBc[] All =
    [
        new() { Name = "basket", Surface = "customer" },
        new() { Name = "order", Surface = "customer" },
        new() { Name = "catalog-admin", Surface = "admin" },
    ];

    [Fact]
    public void ForSurface_Customer_ExcludesAdmin()
    {
        var names = SurfaceFilter.ForSurface(All, SurfaceFilter.Customer).Select(d => d.Name).ToList();
        names.ShouldBe(["basket", "order"]);
    }

    [Fact]
    public void ForSurface_Admin_ExcludesCustomer()
    {
        var names = SurfaceFilter.ForSurface(All, SurfaceFilter.Admin).Select(d => d.Name).ToList();
        names.ShouldBe(["catalog-admin"]);
    }

    [Fact]
    public void ForSurface_IsCaseInsensitive()
        => SurfaceFilter.ForSurface(All, "CUSTOMER").Count().ShouldBe(2);

    [Theory]
    [InlineData("/mcp-admin", "admin")]
    [InlineData("/mcp-admin/x", "admin")]
    [InlineData("/mcp", "customer")]
    [InlineData("/mcp/x", "customer")]
    public void FromPath_MapsSurface(string path, string expected)
        => SurfaceFilter.FromPath(path).ShouldBe(expected);
}