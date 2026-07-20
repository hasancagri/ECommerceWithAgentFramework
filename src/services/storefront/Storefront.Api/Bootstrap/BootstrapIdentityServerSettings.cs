namespace Storefront.Api.Bootstrap;

// WebApp/Authentication/IdentityServerSettings.cs deseniyle ayni — Storefront'un bootstrap
// hosted service'i (research.md madde 5) icin client_credentials token uretimi tek kaynagi.
public class BootstrapIdentityServerSettings
{
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    public string TokenEndpoint => $"{Authority.TrimEnd('/')}/connect/token";
}