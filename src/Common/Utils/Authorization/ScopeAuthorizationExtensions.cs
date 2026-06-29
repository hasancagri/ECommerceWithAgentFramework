using System.Security.Claims;

namespace Common;

// MCP tool'lari icinde per-tool yetki kontrolu: forward edilen token'daki "scope" claim'i.
// JWT auth MapInboundClaims=false ile claim tipini "scope" birakir; policy'ler de
// RequireClaim("scope", x) kullanir => ayni semantik. Hem coklu "scope" claim'i hem de
// bosluklu tek "scope" claim'i tolere edilir.
public static class ScopeAuthorizationExtensions
{
    public static bool HasScope(this ClaimsPrincipal? user, string scope)
    {
        if (user is null)
            return false;

        foreach (var claim in user.FindAll("scope"))
            if (claim.Value == scope || claim.Value.Split(' ').Contains(scope))
                return true;

        return false;
    }
}