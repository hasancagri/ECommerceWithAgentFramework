using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;

namespace WebApp.Authentication;

// Token EDINME sorumlulugu burada toplanir: M2M (client_credentials) ve refresh exchange.
// Token'i isteklere EKLEME isi DelegatingHandler'larda; bu servis sadece token URETIR.
public class TokenService(IHttpClientFactory httpClientFactory, IdentityServerSettings settings)
{
    // Anonim (login'siz) gezinme icin public okuma scope'lari.
    // ecommerce.bff client'inin client_credentials ile alabilecegi API scope'lari olmali.
    private const string ReadScopes =
        "catalog.read discount.read stock.read storefront.read";

    // Kullanici YOKKEN (anonim) okuma icin uygulamanin kendi kimligi.
    public async Task<TokenResponse> GetClientAccessTokenAsync()
    {
        var client = httpClientFactory.CreateClient("identity");
        return await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = settings.TokenEndpoint,
            ClientId = settings.ClientId,
            ClientSecret = settings.ClientSecret,
            Scope = ReadScopes,
        });
    }

    // Access token suresi dolunca, refresh token ile yeni token seti al.
    public async Task<TokenResponse> GetTokensByRefreshTokenAsync(string refreshToken)
    {
        var client = httpClientFactory.CreateClient("identity");
        return await client.RequestRefreshTokenAsync(new RefreshTokenRequest
        {
            Address = settings.TokenEndpoint,
            ClientId = settings.ClientId,
            ClientSecret = settings.ClientSecret,
            RefreshToken = refreshToken,
        });
    }

    // Yenilenen token'lari cookie'ye geri yazmak icin AuthenticationProperties hazirla.
    public AuthenticationProperties CreateAuthenticationProperties(TokenResponse tokenResponse)
    {
        var properties = new AuthenticationProperties();
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn);
        properties.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = tokenResponse.AccessToken! },
            new AuthenticationToken { Name = "refresh_token", Value = tokenResponse.RefreshToken! },
            new AuthenticationToken { Name = "expires_at", Value = expiresAt.ToString("o") },
        });
        return properties;
    }
}
