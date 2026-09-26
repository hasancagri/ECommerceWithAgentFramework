namespace Common.Extensions;

// 085 R1: tek gerçek-kaynak BC'nin ConfigureSessionOptions'ıdır — yol-prefix yerine oturumu açan
// token'ın scope claim'lerine bakar. Tool→scope eşlemesi BC'nin kendi *AdminSurface holder'ında
// (tek kaynak; fasada kopyalanmaz, bkz. contracts/mcp-surface.md).
public static class McpScopePruningExtension
{
    // tool ∈ liste ⟺ tool ∉ adminToolScopes ∨ requiredScope ∈ token.scopes — scope başına, hep-ya-hiç DEĞİL.
    // Token'sız/anonim kullanıcı (ClaimsPrincipal boş) hiçbir admin tool'u göremez.
    public static bool IsToolVisible(string toolName, IReadOnlyDictionary<string, string> adminToolScopes, ClaimsPrincipal user)
        => !adminToolScopes.TryGetValue(toolName, out var requiredScope) || user.HasClaim("scope", requiredScope);
}