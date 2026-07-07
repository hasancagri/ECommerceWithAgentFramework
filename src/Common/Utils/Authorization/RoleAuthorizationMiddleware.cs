using System.Reflection;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace Common.Utils.Authorization;

// Wolverine middleware: her handler'dan ONCE calisir. Mesaj tipinde [RequiredRole] varsa,
// forward edilen token'daki "role" claim'ini kontrol eder; yoksa UnauthorizedAccessException
// (handler calismaz). REST ve MCP ikisi de bus.InvokeAsync ile ayni handler'a ugradigi icin
// yetki TEK NOKTADA kontrol edilir. ScopeAuthorizationMiddleware'in rol ikizi.
public static class RoleAuthorizationMiddleware
{
    public static void Before(Envelope envelope, IHttpContextAccessor http)
    {
        var role = envelope.Message?.GetType()
            .GetCustomAttribute<RequiredRoleAttribute>()?.Role;
        if (role is null)
            return;

        // MapInboundClaims=false oldugu icin "role" claim'i ham; HasClaim dogrudan calisir.
        if (http.HttpContext?.User.HasClaim("role", role) != true)
            throw new UnauthorizedAccessException($"Required role missing: {role}");
    }
}