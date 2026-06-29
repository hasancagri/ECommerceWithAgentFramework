using System.Reflection;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace Common.Utils.Authorization;

// Wolverine middleware: her handler'dan ONCE calisir. Mesaj tipinde [RequiredScope] varsa,
// forward edilen token'daki "scope" claim'ini kontrol eder; yoksa UnauthorizedAccessException
// (handler calismaz). REST ve MCP ikisi de bus.InvokeAsync ile ayni handler'a ugradigi icin
// yetki TEK NOKTADA kontrol edilir.
public static class ScopeAuthorizationMiddleware
{
    public static void Before(Envelope envelope, IHttpContextAccessor http)
    {
        var scope = envelope.Message?.GetType()
            .GetCustomAttribute<RequiredScopeAttribute>()?.Scope;
        if (scope is null)
            return;

        if (http.HttpContext?.User.HasScope(scope) != true)
            throw new UnauthorizedAccessException($"Required scope missing: {scope}");
    }
}