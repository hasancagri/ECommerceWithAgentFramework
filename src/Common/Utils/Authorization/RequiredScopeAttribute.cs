namespace Common.Utils.Authorization;

// Bir komut/sorgu'nun (Wolverine message) calismasi icin gereken scope'u isaretler.
// ScopeAuthorizationMiddleware bunu okuyup token'daki scope ile karsilastirir.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequiredScopeAttribute(string scope) : Attribute
{
    public string Scope { get; } = scope;
}