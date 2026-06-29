using Duende.IdentityModel.Client;

namespace AgentOrchestrator;

public interface IClientCredentialsTokenProvider
{
    Task<string?> GetTokenAsync(CancellationToken ct = default);
}

// m2m (client_credentials) token'i edinir + suresine gore RAM'de cache'ler.
// Anonim MCP cagrilari icin uygulamanin kendi kimligi (m2m.client, read scope'lari).
public sealed class ClientCredentialsTokenProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<ClientCredentialsTokenProvider> logger) : IClientCredentialsTokenProvider
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _token;

        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _token;

            var authority = configuration["IdentityServer:Authority"]
                ?? throw new InvalidOperationException("IdentityServer:Authority is not set");

            var client = httpClientFactory.CreateClient("identity");
            var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = $"{authority.TrimEnd('/')}/connect/token",
                ClientId = configuration["IdentityServer:ClientId"] ?? "m2m.client",
                ClientSecret = configuration["IdentityServer:ClientSecret"] ?? "dev-secret",
                Scope = configuration["IdentityServer:Scope"] ?? "catalog.read",
            }, ct);

            if (response.IsError)
            {
                logger.LogWarning("m2m token alinamadi: {Error}", response.Error);
                return null;
            }

            _token = response.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, response.ExpiresIn - 60));
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }
}