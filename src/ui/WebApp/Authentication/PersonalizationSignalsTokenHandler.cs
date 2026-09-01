using System.Net.Http.Headers;
using Duende.IdentityModel.Client;

namespace WebApp.Authentication;

// 048: gezinme sinyali gonderimi anonim kullaniciyi da kapsar (user token garanti degil) → WebApp
// client_credentials MAKINE token'i (webapp-signals; scope personalization.ingest) ekler. Token
// static cache'lenir, suresine 30 sn kala yenilenir (SagaTokenHandler emsali).
public sealed class PersonalizationSignalsTokenHandler(
    IHttpClientFactory httpClientFactory,
    IdentityServerSettings identity,
    SignalsAuth signalsAuth) : DelegatingHandler
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static string? _token;
    private static DateTimeOffset _expiresAt;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-30))
            return _token;

        await Gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt.AddSeconds(-30))
                return _token;

            var client = httpClientFactory.CreateClient("identity");
            var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = identity.TokenEndpoint,
                ClientId = signalsAuth.ClientId,
                ClientSecret = signalsAuth.ClientSecret,
                // 053: ingest (sinyal yaz) + read (zevk profili oku) — ikisi de reco.trainer audience.
                Scope = "personalization.ingest personalization.read",
            }, ct);

            if (response.IsError)
                throw new InvalidOperationException($"webapp-signals token alinamadi: {response.Error}");

            _token = response.AccessToken!;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn > 0 ? response.ExpiresIn : 3600);
            return _token;
        }
        finally
        {
            Gate.Release();
        }
    }
}