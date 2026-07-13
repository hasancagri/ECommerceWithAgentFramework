using Duende.IdentityModel.Client;

namespace ProductEnrichmentAgent;

// Headless worker'in gelen HTTP istegi yoktur; ChatAgent'in token-forward deseni burada islemez.
// Bu handler enrichment.agent m2m kimligiyle Identity.Server'dan bir client_credentials token'i alir,
// sureyle onbellekler ve MCP'ye giden her istege Authorization: Bearer olarak ekler. Yetki, downstream
// servislerdeki [RequiredScope] + ScopeAuthorizationMiddleware ile zorlanir.
public sealed class ClientCredentialsTokenHandler(
    IHttpClientFactory httpClientFactory,
    IOptions<IdentityOption> identityOptions,
    ILogger<ClientCredentialsTokenHandler> logger) : DelegatingHandler
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        // 60 sn tampon: son anda gecerliligini yitiren token'i yeniler.
        if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _accessToken;

        await _lock.WaitAsync(ct);
        try
        {
            if (_accessToken is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _accessToken;

            var options = identityOptions.Value;
            var client = httpClientFactory.CreateClient("identity");

            var disco = await client.GetDiscoveryDocumentAsync(options.Address, ct);
            if (disco.IsError)
            {
                logger.LogError("Identity discovery basarisiz: {Error}", disco.Error);
                return null;
            }

            var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret
            }, ct);

            if (response.IsError)
            {
                logger.LogError("Token alinamadi: {Error}", response.Error);
                return null;
            }

            _accessToken = response.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(response.ExpiresIn - 60, 30));
            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }
}