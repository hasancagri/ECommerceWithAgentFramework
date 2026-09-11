using System.Net.Http.Headers;

namespace Ucp.Api.Grpc;

/// <summary>
/// UCP → Order gRPC'sine client_credentials makine token'ı (scope <c>order.write</c>) ekler. UCP session
/// complete arka planda koşar (kullanıcı sentetik, taşınacak bearer yok). Token statik cache'lenir,
/// süresine 30 sn kala yenilenir (SagaTokenHandler emsali). ScheduleAsync/gRPC adımına takılır.
/// </summary>
public sealed class OrderTokenHandler(IdentityOption identity, UcpServiceAuth auth) : DelegatingHandler
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

            using var http = new HttpClient();
            using var response = await http.PostAsync($"{identity.Address}/connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = auth.ClientId,
                    ["client_secret"] = auth.ClientSecret,
                    ["scope"] = AuthorizationScopes.OrderWrite
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