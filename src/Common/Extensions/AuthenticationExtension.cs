using Common.Options;
using Common.Utils.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Common.Extensions;

public static class AuthenticationExtension
{
    // Identity.Server'in tanimladigi tum API scope'lari. Policy adi = scope adi.
    // Her servis icin read/write scope ikilisi; her servis kendi audience'ina ait token'i
    // dogrular ve sadece ilgili scope policy'leri kullanilir.
    private static readonly string[] ApiScopes =
    [
        AuthorizationScopes.CatalogRead,
        AuthorizationScopes.CatalogWrite,

        AuthorizationScopes.BasketRead,
        AuthorizationScopes.BasketWrite,

        AuthorizationScopes.OrderRead,
        AuthorizationScopes.OrderWrite,

        AuthorizationScopes.PaymentRead,
        AuthorizationScopes.PaymentWrite,

        AuthorizationScopes.DiscountRead,
        AuthorizationScopes.DiscountWrite,
    ];

    public static IServiceCollection AddAuthenticationAndAuthorizationExtension(this IServiceCollection services,
        IConfiguration configuration)
    {
        var identityOptions = configuration.GetSection(nameof(IdentityOption)).Get<IdentityOption>()!;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = identityOptions.Address;
                options.Audience = identityOptions.Audience;
                options.RequireHttpsMetadata = false;

                // Claim'leri token'daki haliyle birak (scope/role/email kisa adlariyla).
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidateIssuer = true,
                };

                options.AutomaticRefreshInterval = TimeSpan.FromHours(24);
                options.RefreshInterval = TimeSpan.FromSeconds(30);
            });

        services.AddAuthorization(options =>
        {
            // Her scope icin policy: gecerli (authenticated) token + ilgili "scope" claim'i.
            foreach (var scope in ApiScopes)
                options.AddPolicy(scope, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", scope);
                });
        });

        return services;
    }
}