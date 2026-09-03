using System.Text.Json.Serialization;

namespace Identity.Server.Connect;

// 061: RFC 7591 Dynamic Client Registration — Claude Code/Desktop istemci kaydı.
// Anonim uç (kayıt anında kimlik yok); sınırlar: izinli redirect kalıpları + kapalı scope
// demeti + yalnız public client (DcrRequestValidator). Rate-limit tünel/public aşamasının işi.
public static class RegisterEndpoint
{
    public static void MapRegisterEndpoint(this WebApplication app) =>
        app.MapPost("/connect/register", HandleAsync);

    private static async Task<IResult> HandleAsync(
        DcrRequestBody body,
        IOpenIddictApplicationManager applicationManager,
        CancellationToken ct)
    {
        var result = DcrRequestValidator.Validate(new DcrRequest(
            body.ClientName, body.RedirectUris, body.GrantTypes,
            body.TokenEndpointAuthMethod, body.Scope));

        if (!result.IsValid)
            return Results.BadRequest(new DcrErrorBody(result.Error!, result.ErrorDescription!));

        var clientId = "dcr-" + Guid.NewGuid().ToString("N");
        var displayName = string.IsNullOrWhiteSpace(body.ClientName) ? "External Agent" : body.ClientName;

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = displayName,
            ClientType = ClientTypes.Public,
            // Her yeni dış istemci kullanıcı onayı ister (R6); seed client'lar Implicit kalır.
            ConsentType = ConsentTypes.Explicit,
        };

        descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(Permissions.Endpoints.Token);
        descriptor.Permissions.Add(Permissions.Endpoints.Revocation);
        descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);

        if (result.GrantTypes.Contains(GrantTypes.RefreshToken))
            descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);

        foreach (var scope in result.Scopes)
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);

        foreach (var uri in result.RedirectUris)
            descriptor.RedirectUris.Add(new Uri(uri));

        await applicationManager.CreateAsync(descriptor, ct);

        return Results.Created($"/connect/register/{clientId}", new DcrResponseBody
        {
            ClientId = clientId,
            ClientName = displayName,
            RedirectUris = result.RedirectUris,
            GrantTypes = result.GrantTypes,
            ResponseTypes = ["code"],
            TokenEndpointAuthMethod = "none",
            Scope = string.Join(' ', result.Scopes),
        });
    }

    // RFC 7591 istek gövdesi (Claude'un gönderdiği alt küme; bilinmeyen alanlar yok sayılır).
    public sealed record DcrRequestBody(
        [property: JsonPropertyName("client_name")] string? ClientName,
        [property: JsonPropertyName("redirect_uris")] string[]? RedirectUris,
        [property: JsonPropertyName("grant_types")] string[]? GrantTypes,
        [property: JsonPropertyName("response_types")] string[]? ResponseTypes,
        [property: JsonPropertyName("token_endpoint_auth_method")] string? TokenEndpointAuthMethod,
        [property: JsonPropertyName("scope")] string? Scope);

    public sealed record DcrErrorBody(
        [property: JsonPropertyName("error")] string Error,
        [property: JsonPropertyName("error_description")] string ErrorDescription);

    public sealed class DcrResponseBody
    {
        [JsonPropertyName("client_id")] public required string ClientId { get; init; }
        [JsonPropertyName("client_name")] public required string ClientName { get; init; }
        [JsonPropertyName("redirect_uris")] public required IReadOnlyList<string> RedirectUris { get; init; }
        [JsonPropertyName("grant_types")] public required IReadOnlyList<string> GrantTypes { get; init; }
        [JsonPropertyName("response_types")] public required IReadOnlyList<string> ResponseTypes { get; init; }
        [JsonPropertyName("token_endpoint_auth_method")] public required string TokenEndpointAuthMethod { get; init; }
        [JsonPropertyName("scope")] public required string Scope { get; init; }
    }
}