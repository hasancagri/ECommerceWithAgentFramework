namespace ProductEnrichmentAgent.Options;

// Enrichment agent'in client-credentials kimligi (Identity.Server Config.cs 'enrichment.agent').
// appsettings 'IdentityOption' bolumunden Options Pattern ile baglanir. Scope istenmez —
// Duende, client'in AllowedScopes'unu token'a otomatik koyar (tek kaynak Config.cs).
public sealed class IdentityOption
{
    public string Address { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}