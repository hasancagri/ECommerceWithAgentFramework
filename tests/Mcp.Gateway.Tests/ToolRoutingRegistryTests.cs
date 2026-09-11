using Mcp.Gateway.Options;
using Mcp.Gateway.Routing;
using Shouldly;
using Xunit;

namespace Mcp.Gateway.Tests;

// 073 İlke VI: tool-adı → BC yönlendirme kaydı (saf) test-first.
public class ToolRoutingRegistryTests
{
    private static DownstreamBc Bc(string name) => new() { Name = name, McpUrl = $"http://{name}/mcp" };

    [Fact]
    public void Resolve_ReturnsOwner_ForRegisteredTool()
    {
        var r = new ToolRoutingRegistry();
        r.Add("add_to_cart", Bc("basket")).ShouldBeTrue();

        r.Resolve("add_to_cart")!.Name.ShouldBe("basket");
    }

    [Fact]
    public void Resolve_ReturnsNull_ForUnknownTool()
        => new ToolRoutingRegistry().Resolve("nope").ShouldBeNull();

    [Fact]
    public void Add_DuplicateName_FirstOwnerWins()
    {
        var r = new ToolRoutingRegistry();
        r.Add("place_order", Bc("order")).ShouldBeTrue();
        r.Add("place_order", Bc("other")).ShouldBeFalse(); // ikinci yok sayılır

        r.Resolve("place_order")!.Name.ShouldBe("order");
        r.Count.ShouldBe(1);
    }

    [Fact]
    public void Add_BlankName_Ignored()
        => new ToolRoutingRegistry().Add("", Bc("x")).ShouldBeFalse();
}