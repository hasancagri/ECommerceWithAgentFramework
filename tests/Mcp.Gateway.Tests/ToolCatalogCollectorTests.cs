using Mcp.Gateway.Aggregation;
using Shouldly;
using Xunit;

namespace Mcp.Gateway.Tests;

// 085 R2 İLKE VI: tools/list cache anahtarı (saf) — aynı scope setinin sahibi herkes cache paylaşır;
// sıra farkı/token'sızlık ayrı-anahtar üretmez/üretir doğru davranmalı.
public class ToolCatalogCollectorTests
{
    [Fact]
    public void ScopeFingerprint_NullScopes_ReturnsAnon()
        => ToolCatalogCollector.ScopeFingerprint(null).ShouldBe("anon");

    [Fact]
    public void ScopeFingerprint_EmptyScopes_ReturnsAnon()
        => ToolCatalogCollector.ScopeFingerprint([]).ShouldBe("anon");

    [Fact]
    public void ScopeFingerprint_SameScopesDifferentOrder_ProduceSameKey()
    {
        var a = ToolCatalogCollector.ScopeFingerprint(["basket.write", "catalog.write"]);
        var b = ToolCatalogCollector.ScopeFingerprint(["catalog.write", "basket.write"]);

        a.ShouldBe(b);
    }

    [Fact]
    public void ScopeFingerprint_DifferentScopes_ProduceDifferentKeys()
    {
        var customer = ToolCatalogCollector.ScopeFingerprint(["basket.write"]);
        var admin = ToolCatalogCollector.ScopeFingerprint(["catalog.write"]);

        customer.ShouldNotBe(admin);
    }

    [Fact]
    public void ScopeFingerprint_PartialAdminScope_DistinctFromFullAdminScope()
    {
        var partial = ToolCatalogCollector.ScopeFingerprint(["catalog.read"]);
        var full = ToolCatalogCollector.ScopeFingerprint(["catalog.read", "catalog.write"]);

        partial.ShouldNotBe(full);
    }
}