using Microsoft.Extensions.Options;
using OpenIddict.Core;

namespace Identity.Server.Connect;

// 070: YALNIZ seed'li yönetim istemcisi (external-admin-agent) için RFC 8252 §7.3 loopback
// redirect muafiyeti — MCP Inspector/CLI istemcileri dinamik portlu http://localhost|127.0.0.1
// callback'i kullanır, seed'e port port yazılamaz. Diğer TÜM istemciler (DCR dahil) birebir
// eşleşme kuralında kalır; DcrRequestValidator loopback'i zaten KAYIT anında sabitler.
public sealed class AdminAgentApplicationManager<TApplication>(
    IOpenIddictApplicationCache<TApplication> cache,
    ILogger<OpenIddictApplicationManager<TApplication>> logger,
    IOptionsMonitor<OpenIddictCoreOptions> options,
    IOpenIddictApplicationStore<TApplication> store)
    : OpenIddictApplicationManager<TApplication>(cache, logger, options, store)
    where TApplication : class
{
    public override async ValueTask<bool> ValidateRedirectUriAsync(
        TApplication application, string uri, CancellationToken cancellationToken = default)
    {
        if (await base.ValidateRedirectUriAsync(application, uri, cancellationToken))
            return true;

        // 070 admin + 073 müşteri fasad agent'ı: ikisi de loopback (mcp-remote dinamik port) kullanır.
        var clientId = await GetClientIdAsync(application, cancellationToken);
        if (clientId is not (Config.ExternalAdminAgentClientId or Config.ExternalCustomerAgentClientId))
            return false;

        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
               && parsed.Scheme == Uri.UriSchemeHttp
               && parsed.Host is "localhost" or "127.0.0.1";
    }
}