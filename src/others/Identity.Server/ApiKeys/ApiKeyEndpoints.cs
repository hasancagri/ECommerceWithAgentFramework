using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Identity.Server;

// İç introspection + admin (issue/revoke) uçları.
// v1 koruması: paylaşılan X-Internal-Secret header (D8). Üretimde admin uçları için
// apikeys.manage scope zorlaması hedeflenir (Config'te tanımlı) — sertleştirme ayrı iş.
public static class ApiKeyEndpoints
{
    public const string InternalSecretHeader = "X-Internal-Secret";

    public static void MapApiKeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/keys");

        // Resolve (servislerin ApiKey handler'ı çağırır)
        group.MapPost("/resolve", async (
            ResolveRequest body, HttpContext http, ApiKeyService service, IConfiguration config, CancellationToken ct) =>
        {
            if (!IsInternalCallAuthorized(http, config))
                return Results.Unauthorized();

            var resolved = await service.ResolveAsync(body.Key, ct);
            return resolved is null
                ? Results.Unauthorized()
                : Results.Ok(new ResolveResponse(resolved.UserId, resolved.Email, null, null, resolved.Scopes));
        });

        // Issue (admin)
        group.MapPost("/", async (
            IssueRequest body, HttpContext http, ApiKeyService service, IConfiguration config, CancellationToken ct) =>
        {
            if (!IsInternalCallAuthorized(http, config))
                return Results.Unauthorized();

            var (entity, rawKey) = await service.IssueAsync(body.UserId, body.Name, ct);
            return Results.Created($"/api/keys/{entity.Id}",
                new IssueResponse(entity.Id, rawKey, entity.UserId, entity.Name, entity.CreatedAt));
        });

        // Revoke (admin) — idempotent
        group.MapPost("/{id:guid}/revoke", async (
            Guid id, HttpContext http, ApiKeyService service, IConfiguration config, CancellationToken ct) =>
        {
            if (!IsInternalCallAuthorized(http, config))
                return Results.Unauthorized();

            var found = await service.RevokeAsync(id, ct);
            return found ? Results.NoContent() : Results.NotFound();
        });
    }

    private static bool IsInternalCallAuthorized(HttpContext http, IConfiguration config)
    {
        var expected = config["ApiKeyAuth:InternalSecret"];
        if (string.IsNullOrEmpty(expected))
            return false;

        return http.Request.Headers.TryGetValue(InternalSecretHeader, out var provided)
               && provided.ToString() == expected;
    }

    private record ResolveRequest(string Key);
    private record ResolveResponse(string UserId, string? Email, string? GivenName, string? FamilyName, string[] Scopes);
    private record IssueRequest(string UserId, string? Name);
    private record IssueResponse(Guid Id, string Key, string UserId, string? Name, DateTime CreatedAt);
}