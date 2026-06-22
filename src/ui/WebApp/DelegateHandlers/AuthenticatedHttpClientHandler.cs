using System.Net;
using System.Security.Claims;
using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using WebApp.Services;

namespace WebApp.DelegateHandlers;

// Kullanici LOGIN ISE: user access_token'i istege ekler.
// Yanit 401 ise: refresh_token ile yeniler, cookie'yi gunceller, istegi tekrar dener.
// Kullanici login degilse: dokunmadan gecer (M2M token'i dis handler eklemistir).
public class AuthenticatedHttpClientHandler(
    IHttpContextAccessor httpContextAccessor,
    TokenService tokenService) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
            return await base.SendAsync(request, cancellationToken);

        // Login degilse bu handler is yapmaz; M2M yolundan gidilir.
        if (httpContext.User.Identity?.IsAuthenticated != true)
            return await base.SendAsync(request, cancellationToken);

        var accessToken = await httpContext.GetTokenAsync(OpenIdConnectParameterNames.AccessToken);
        if (string.IsNullOrEmpty(accessToken))
            throw new UnauthorizedAccessException("Access token bulunamadi.");

        request.SetBearerToken(accessToken);
        var response = await base.SendAsync(request, cancellationToken);

        // 401 degilse isimiz bitti.
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // 401 → refresh token ile yeni token almayi dene.
        var refreshToken = await httpContext.GetTokenAsync(OpenIdConnectParameterNames.RefreshToken);
        if (string.IsNullOrEmpty(refreshToken))
            throw new UnauthorizedAccessException("Refresh token bulunamadi.");

        var tokenResponse = await tokenService.GetTokensByRefreshTokenAsync(refreshToken);
        if (tokenResponse.IsError)
            throw new UnauthorizedAccessException("Access token yenilenemedi.");

        // Yeni token'lari cookie'ye geri yaz (mevcut claim'leri koruyarak yeniden SignIn).
        var properties = tokenService.CreateAuthenticationProperties(tokenResponse);
        var identity = new ClaimsIdentity(httpContext.User.Claims,
            CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role);
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), properties);

        // Yeni access token ile istegi tekrar dene.
        // Not (ogrenme projesi): ayni request POST govdesiyle yeniden gonderiliyor; uretimde
        // istek klonlanmali. Bu akista 401 cogunlukla GET'te (govdesiz) tetiklenir.
        request.SetBearerToken(tokenResponse.AccessToken!);
        return await base.SendAsync(request, cancellationToken);
    }
}