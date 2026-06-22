namespace WebApp.Settings;

// Identity.Server baglantisi icin tek config kaynagi (appsettings.json -> "IdentityServer").
// Hem OIDC login (Program.cs) hem M2M/refresh (TokenService) buradan okur.
public class IdentityServerSettings
{
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    // Token endpoint Authority'den turetilir; ayrica yazmaya gerek yok.
    public string TokenEndpoint => $"{Authority.TrimEnd('/')}/connect/token";
}
