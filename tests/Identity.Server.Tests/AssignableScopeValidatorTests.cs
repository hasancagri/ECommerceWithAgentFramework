using Identity.Server.Rbac;
using Shouldly;
using Xunit;

namespace Identity.Server.Tests;

// INV-1: KnownScopes dışı bir scope bir role eşlenemez (serbest metin yasak).
public class AssignableScopeValidatorTests
{
    private static readonly HashSet<string> Known = ["catalog.write", "basket.write", "order.read"];

    [Fact]
    public void FindUnknown_ReturnsScopesNotInRegistry()
    {
        var scopes = new[] { "catalog.write", "made.up", "typo.wrte" };

        var unknown = AssignableScopeValidator.FindUnknown(scopes, Known);

        unknown.ShouldBe(new[] { "made.up", "typo.wrte" }, ignoreOrder: true);
    }

    [Fact]
    public void AllKnown_TrueWhenEveryScopeInRegistry()
    {
        AssignableScopeValidator.AllKnown(new[] { "catalog.write", "basket.write" }, Known).ShouldBeTrue();
    }

    [Fact]
    public void AllKnown_FalseWhenAnyScopeUnknown()
    {
        AssignableScopeValidator.AllKnown(new[] { "catalog.write", "nope" }, Known).ShouldBeFalse();
    }

    [Fact]
    public void AllKnown_TrueForEmpty()
    {
        AssignableScopeValidator.AllKnown([], Known).ShouldBeTrue();
    }
}