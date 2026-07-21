using System.Security.Cryptography;
using System.Text;

namespace Identity.Server.ApiKeys;

// İç introspection + admin (issue/revoke) uçları.
// - resolve: saf iç introspection → paylaşılan X-Internal-Secret header (servisler her write'ta çağırır).
// - issue/revoke: admin → apikeys.manage scope zorunlu (Bearer; apikeys.admin client_credentials).
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

        // Issue (admin) — apikeys.manage scope zorunlu
        group.MapPost("/", async (
            IssueRequest body, ApiKeyService service, CancellationToken ct) =>
        {
            var (entity, rawKey) = await service.IssueAsync(body.UserId, body.Name, ct);
            return Results.Created($"/api/keys/{entity.Id}",
                new IssueResponse(entity.Id, rawKey, entity.UserId, entity.Name, entity.CreatedAt));
        }).RequireAuthorization("apikeys.manage");

        // Revoke (admin) — idempotent, apikeys.manage scope zorunlu
        group.MapPost("/{id:guid}/revoke", async (
            Guid id, ApiKeyService service, CancellationToken ct) =>
        {
            var found = await service.RevokeAsync(id, ct);
            return found ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("apikeys.manage");
    }

    private static bool IsInternalCallAuthorized(HttpContext http, IConfiguration config)
    {
        var expected = config["ApiKeyAuth:InternalSecret"];
        if (string.IsNullOrEmpty(expected))
            return false;

        if (!http.Request.Headers.TryGetValue(InternalSecretHeader, out var provided))
            return false;

        // Sabit-zamanlı karşılaştırma: her iki değerin SHA-256'sını FixedTimeEquals ile karşılaştır.
        // Eşit-uzunluk (32 bayt) → uzunluk sızıntısı yok; `==` yerine timing-saldırısına dayanıklı.
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided.ToString()));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }

    private record ResolveRequest(string Key);
    private record ResolveResponse(string UserId, string? Email, string? GivenName, string? FamilyName, string[] Scopes);
    private record IssueRequest(string UserId, string? Name);
    private record IssueResponse(Guid Id, string Key, string UserId, string? Name, DateTime CreatedAt);
}