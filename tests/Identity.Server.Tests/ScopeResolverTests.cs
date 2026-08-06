using Identity.Server.Rbac;
using Shouldly;
using Xunit;

namespace Identity.Server.Tests;

// INV-6: token'a yazılan API scope'ları = requested ∩ (rol demeti ∪ her-zaman-izinli).
public class ScopeResolverTests
{
    private static readonly HashSet<string> IdentityScopes =
        ["openid", "profile", "email", "roles", "offline_access"];

    [Fact]
    public void Resolve_KeepsOnlyRequestedScopesInRoleBundle()
    {
        var requested = new[] { "basket.write", "catalog.write", "storefront.read" };
        var bundle = new HashSet<string> { "basket.write", "storefront.read" };

        var granted = ScopeResolver.Resolve(requested, bundle, IdentityScopes);

        granted.ShouldBe(new[] { "basket.write", "storefront.read" }, ignoreOrder: true);
        granted.ShouldNotContain("catalog.write");
    }

    [Fact]
    public void Resolve_AlwaysAllowsIdentityScopes()
    {
        var requested = new[] { "openid", "offline_access", "basket.write" };
        var bundle = new HashSet<string> { "basket.write" };

        var granted = ScopeResolver.Resolve(requested, bundle, IdentityScopes);

        granted.ShouldContain("openid");
        granted.ShouldContain("offline_access");
        granted.ShouldContain("basket.write");
    }

    [Fact]
    public void Resolve_DropsScopesNotRequestedEvenIfInBundle()
    {
        var requested = new[] { "basket.write" };
        var bundle = new HashSet<string> { "basket.write", "order.write" };

        var granted = ScopeResolver.Resolve(requested, bundle, IdentityScopes);

        granted.ShouldNotContain("order.write");
    }

    [Fact]
    public void Resolve_DropsUnknownOrRevokedScopes()
    {
        // Bundle'da olmayan / KnownScopes'tan düşmüş scope token'a yazılmaz.
        var requested = new[] { "basket.write", "made.up.scope" };
        var bundle = new HashSet<string> { "basket.write" };

        var granted = ScopeResolver.Resolve(requested, bundle, IdentityScopes);

        granted.ShouldBe(new[] { "basket.write" });
    }

    [Fact]
    public void Resolve_Deduplicates()
    {
        var requested = new[] { "basket.write", "basket.write" };
        var bundle = new HashSet<string> { "basket.write" };

        ScopeResolver.Resolve(requested, bundle, IdentityScopes).Count.ShouldBe(1);
    }
}