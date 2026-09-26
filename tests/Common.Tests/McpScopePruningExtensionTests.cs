using System.Security.Claims;
using Common.Extensions;
using Shouldly;
using Xunit;

namespace Common.Tests;

// 085 R1/T008 İLKE VI: saf budama kuralı test-first — tool ∈ liste ⟺ tool ∉ adminToolScopes
// ∨ requiredScope ∈ token.scopes (contracts/mcp-surface.md).
public class McpScopePruningExtensionTests
{
    private static readonly Dictionary<string, string> AdminScopes = new()
    {
        ["admin_set_stock"] = "stock.write",
        ["admin_list_all_stock"] = "stock.write",
    };

    private static ClaimsPrincipal UserWithScopes(params string[] scopes)
        => new(new ClaimsIdentity(scopes.Select(s => new Claim("scope", s))));

    [Fact]
    public void NonAdminTool_AlwaysVisible()
        => McpScopePruningExtension.IsToolVisible("get_stock", AdminScopes, UserWithScopes()).ShouldBeTrue();

    [Fact]
    public void AdminTool_VisibleWhenScopePresent()
        => McpScopePruningExtension.IsToolVisible("admin_set_stock", AdminScopes, UserWithScopes("stock.write")).ShouldBeTrue();

    [Fact]
    public void AdminTool_HiddenWithoutScope()
        => McpScopePruningExtension.IsToolVisible("admin_set_stock", AdminScopes, UserWithScopes()).ShouldBeFalse();

    [Fact]
    public void AdminTool_HiddenForAnonymousUser()
        => McpScopePruningExtension.IsToolVisible("admin_set_stock", AdminScopes, new ClaimsPrincipal()).ShouldBeFalse();

    [Fact]
    public void PartialAdminScope_OnlyMatchingScopeToolsVisible()
    {
        var scopes = new Dictionary<string, string> { ["admin_a"] = "catalog.read", ["admin_b"] = "catalog.write" };
        var user = UserWithScopes("catalog.read");

        McpScopePruningExtension.IsToolVisible("admin_a", scopes, user).ShouldBeTrue();
        McpScopePruningExtension.IsToolVisible("admin_b", scopes, user).ShouldBeFalse();
    }

    [Fact]
    public void ToolNotInAdminMap_AlwaysVisibleRegardlessOfScopes()
        => McpScopePruningExtension.IsToolVisible("totally_unknown_tool", AdminScopes, UserWithScopes()).ShouldBeTrue();
}
