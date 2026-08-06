namespace Identity.Server.Rbac;

// 030 RBAC (saf): token verme anında granted API scope'ları = requested ∩ (rol demeti ∪
// her-zaman-izinli). Kimlik scope'ları (openid/profile/email/roles/offline_access) her zaman
// geçer; rol demetinde olmayan / KnownScopes'tan düşmüş scope token'a YAZILMAZ (INV-6).
public static class ScopeResolver
{
    public static IReadOnlyList<string> Resolve(
        IEnumerable<string> requested,
        ISet<string> roleBundle,
        ISet<string> alwaysAllow) =>
        [.. requested
            .Where(s => alwaysAllow.Contains(s) || roleBundle.Contains(s))
            .Distinct()];
}