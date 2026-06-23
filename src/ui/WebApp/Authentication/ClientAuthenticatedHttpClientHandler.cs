using Duende.IdentityModel.Client;

namespace WebApp.Authentication;

// Kullanici LOGIN DEGILSE: uygulamanin kendi M2M (client_credentials) token'ini ekler.
// Login ise: dokunmadan gecer; user token'i ic handler (Authenticated) ekleyecek.
public class ClientAuthenticatedHttpClientHandler(
    IHttpContextAccessor httpContextAccessor,
    TokenService tokenService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;

        // Login ise M2M'e gerek yok; ic handler user token'i ekler.
        if (httpContext?.User.Identity?.IsAuthenticated == true)
            return await base.SendAsync(request, cancellationToken);

        var tokenResponse = await tokenService.GetClientAccessTokenAsync();
        if (tokenResponse.IsError)
            throw new UnauthorizedAccessException($"M2M token alinamadi: {tokenResponse.Error}");

        request.SetBearerToken(tokenResponse.AccessToken!);
        return await base.SendAsync(request, cancellationToken);
    }
}