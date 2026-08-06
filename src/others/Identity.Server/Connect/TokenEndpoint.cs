using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Server.Connect;

// /connect/token — client_credentials + authorization_code(+PKCE) + refresh_token exchange.
public static class TokenEndpoint
{
    public static void MapTokenEndpoint(this WebApplication app) =>
        app.MapPost("/connect/token", HandleAsync);

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOpenIddictScopeManager scopeManager)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OIDC token isteği çözülemedi.");

        // M2M: sub = client id (servisler CurrentUser.Load ile okur). apikeys.admin + order-saga.
        if (request.IsClientCredentialsGrantType())
        {
            var identity = new ClaimsIdentity(
                TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);

            identity.SetClaim(Claims.Subject, request.ClientId);

            identity.SetScopes(request.GetScopes());

            var resources = new List<string>();
            await foreach (var resource in scopeManager.ListResourcesAsync(identity.GetScopes()))
                resources.Add(resource);
            identity.SetResources(resources);

            identity.SetDestinations(OidcClaimDestinations.GetDestinations);

            return Results.SignIn(new ClaimsPrincipal(identity), null,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // Kullanıcı akışları: OpenIddict'in code/refresh token'da sakladığı principal'ı geri al.
        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = result.Principal
                ?? throw new InvalidOperationException("Saklı principal çözülemedi.");

            // Kullanıcı hâlâ giriş yapabiliyor mu (silinmiş/kilitli değil).
            var user = await userManager.FindByIdAsync(principal.GetClaim(Claims.Subject)!);
            if (user is null || !await signInManager.CanSignInAsync(user))
            {
                return Results.Forbid(
                    new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "Kullanıcı artık giriş yapamıyor.",
                    }),
                    [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            // Destinasyonlar yeniden hesaplanır (refresh'te de claim'ler doğru token'a gitsin).
            var identity = new ClaimsIdentity(principal.Claims,
                TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
            identity.SetDestinations(OidcClaimDestinations.GetDestinations);

            return Results.SignIn(new ClaimsPrincipal(identity), null,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("Desteklenmeyen grant type.");
    }
}