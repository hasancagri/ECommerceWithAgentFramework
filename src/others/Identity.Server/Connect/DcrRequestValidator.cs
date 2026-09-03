namespace Identity.Server.Connect;

// 061: RFC 7591 DCR isteğinin saf doğrulayıcısı (İlke VI test-first birimi).
// Kurallar: specs/061-external-mcp-oauth/contracts/dcr-register.md
public sealed record DcrRequest(
    string? ClientName,
    IReadOnlyList<string>? RedirectUris,
    IReadOnlyList<string>? GrantTypes,
    string? TokenEndpointAuthMethod,
    string? Scope);

public sealed record DcrValidationResult
{
    public bool IsValid { get; init; }
    public string? Error { get; init; }
    public string? ErrorDescription { get; init; }
    public IReadOnlyList<string> RedirectUris { get; init; } = [];
    public IReadOnlyList<string> GrantTypes { get; init; } = [];
    public IReadOnlyList<string> Scopes { get; init; } = [];

    public static DcrValidationResult Fail(string error, string description) =>
        new() { IsValid = false, Error = error, ErrorDescription = description };
}

public static class DcrRequestValidator
{
    public static DcrValidationResult Validate(DcrRequest request)
    {
        if (request.RedirectUris is null || request.RedirectUris.Count == 0)
            return DcrValidationResult.Fail("invalid_redirect_uri", "redirect_uris is required.");

        foreach (var uri in request.RedirectUris)
            if (!IsAllowedRedirectUri(uri))
                return DcrValidationResult.Fail("invalid_redirect_uri",
                    $"redirect_uri is not allowed: {uri}");

        var grants = request.GrantTypes is { Count: > 0 }
            ? request.GrantTypes
            : ExternalAgentDefaults.AllowedGrantTypes;

        foreach (var grant in grants)
            if (!ExternalAgentDefaults.AllowedGrantTypes.Contains(grant))
                return DcrValidationResult.Fail("invalid_client_metadata",
                    $"grant_type is not allowed: {grant}");

        // Yalnız public istemci (PKCE): confidential auth yöntemleri reddedilir.
        if (request.TokenEndpointAuthMethod is not (null or "none"))
            return DcrValidationResult.Fail("invalid_client_metadata",
                "Only public clients are allowed (token_endpoint_auth_method must be \"none\").");

        // scope verilmişse demetle kesişim; verilmemişse demetin tamamı. Demet dışı sessizce düşer.
        var scopes = string.IsNullOrWhiteSpace(request.Scope)
            ? ExternalAgentDefaults.AllScopes
            : request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(ExternalAgentDefaults.AllScopes.Contains)
                .Distinct()
                .ToArray();

        return new DcrValidationResult
        {
            IsValid = true,
            RedirectUris = [.. request.RedirectUris.Distinct()],
            GrantTypes = [.. grants.Distinct()],
            Scopes = scopes,
        };
    }

    // Loopback (http://localhost|127.0.0.1, herhangi port/path) veya Claude callback'i (birebir).
    private static bool IsAllowedRedirectUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme == Uri.UriSchemeHttp && uri.Host is "localhost" or "127.0.0.1")
            return true;

        return ExternalAgentDefaults.AllowedExactRedirectUris.Contains(value);
    }
}