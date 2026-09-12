using System.Net.Http.Headers;
using System.Text.Json;
using Common.Options;
using Payment.Api.Options;

namespace Payment.Api.Http;

// 075: Payment BC S2S makine token'i (client_credentials; customer.read) — HttpContext yok (broker
// handler'ı), kullanıcı bearer'ı taşınamaz. Customer payment-context + merchant-key uçlarına Bearer
// ekler. Token static cache'lenir, süresine 30 sn kala yenilenir (Order.SagaTokenHandler deseni).
public sealed class SagaTokenHandler(IdentityOption identity, SagaAuth sagaAuth) : DelegatingHandler
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

            var authority = identity.Address;
            using var http = new HttpClient();
            using var response = await http.PostAsync($"{authority}/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = sagaAuth.ClientId,
                    ["client_secret"] = sagaAuth.ClientSecret,
                    // Payment BC yalnız ödeme-bağlamı + merchant-key okur → customer.read yeterli.
                    ["scope"] = AuthorizationScopes.CustomerRead
                }), ct);
            response.EnsureSuccessStatusCode();

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            _token = json.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = json.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            return _token;
        }
        finally
        {
            Gate.Release();
        }
    }
}
