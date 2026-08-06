using Identity.Server.Rbac;
using Shouldly;
using Xunit;

namespace Identity.Server.Tests;

// INV-2 (tek rol) + INV-3 (rolsüz kalmama): atama mevcut rol(ler)i değiştirir, hedefi bırakır.
public class RoleAssignmentRulesTests
{
    [Fact]
    public void ApplySingleRole_RemovesOtherRolesAndAddsTarget()
    {
        var plan = RoleAssignmentRules.ApplySingleRole(["customer"], "admin");

        plan.RolesToRemove.ShouldBe(new[] { "customer" });
        plan.RoleToAdd.ShouldBe("admin");
    }

    [Fact]
    public void ApplySingleRole_NoOpWhenAlreadyOnlyTarget()
    {
        var plan = RoleAssignmentRules.ApplySingleRole(["admin"], "admin");

        plan.RolesToRemove.ShouldBeEmpty();
        plan.RoleToAdd.ShouldBeNull(); // zaten hedefte; ekleme yok
    }

    [Fact]
    public void ApplySingleRole_FromNoRoleJustAdds()
    {
        var plan = RoleAssignmentRules.ApplySingleRole([], "customer");

        plan.RolesToRemove.ShouldBeEmpty();
        plan.RoleToAdd.ShouldBe("customer");
    }

    [Fact]
    public void ApplySingleRole_RemovesAllExtraRolesKeepingTarget()
    {
        // Bir kullanıcı yanlışlıkla çok rol taşıyorsa, atama tek role indirger (hedef korunur).
        var plan = RoleAssignmentRules.ApplySingleRole(["customer", "editor", "admin"], "admin");

        plan.RolesToRemove.ShouldBe(new[] { "customer", "editor" }, ignoreOrder: true);
        plan.RoleToAdd.ShouldBeNull(); // admin zaten var
    }

    [Fact]
    public void ApplySingleRole_IsCaseInsensitive()
    {
        var plan = RoleAssignmentRules.ApplySingleRole(["Admin"], "admin");

        plan.RolesToRemove.ShouldBeEmpty();
        plan.RoleToAdd.ShouldBeNull();
    }
}