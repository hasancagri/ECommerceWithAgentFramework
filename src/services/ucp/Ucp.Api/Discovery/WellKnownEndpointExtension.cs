namespace Ucp.Api.Discovery;

/// <summary>
/// 072 US2: keşif adresleri (anonim). <c>/.well-known/ucp</c> business profili + <c>/.well-known/
/// oauth-authorization-server</c> → OpenIddict metadata'sına yönlendir (ayrı üretme; contract).
/// </summary>
public static class WellKnownEndpointExtension
{
    public static void AddUcpWellKnownEndpoints(this WebApplication app)
    {
        app.MapGet("/.well-known/ucp", (UcpPlatformOption platform, UcpSigningOption signing) =>
                Results.Ok(UcpProfile.Build(platform, signing)))
            .WithTags("UcpDiscovery");

        // OAuth metadata: mağazanın kimlik sunucusunun (OpenIddict) ürettiği dokümana yönlendir.
        app.MapGet("/.well-known/oauth-authorization-server", (IdentityOption identity) =>
                Results.Redirect($"{identity.Address.TrimEnd('/')}/.well-known/openid-configuration"))
            .WithTags("UcpDiscovery");
    }
}
