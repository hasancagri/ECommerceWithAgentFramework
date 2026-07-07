namespace Common.Utils.Authorization;

// Bir komut/sorgu'nun (Wolverine message) calismasi icin gereken rolu isaretler.
// RoleAuthorizationMiddleware bunu okuyup token'daki "role" claim'i ile karsilastirir.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequiredRoleAttribute(string role) : Attribute
{
    public string Role { get; } = role;
}