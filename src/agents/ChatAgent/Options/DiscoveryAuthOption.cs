namespace ChatAgent.Options;

// Açılış MCP keşfinin makine kimliği (client_credentials, chat-agent-discovery) — appsettings
// "DiscoveryAuth". 061 korumalı /mcp transport'ları kimlik ister; keşif ListTools bu token'la
// geçer. IdentityAddress Program.cs'te service discovery'den doldurulur (Options istisnası).
// Section yoksa keşif anonim kalır (graceful-degrade; korumalı MCP'ler tool'suz atlanır).
public class DiscoveryAuthOption
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    // Yalnız audience (aud) üretimi için salt-read scope'lar; bu token'la tool çağrılmaz.
    public string Scope { get; set; } = "";

    public string IdentityAddress { get; set; } = "";
    public string TokenEndpoint => $"{IdentityAddress.TrimEnd('/')}/connect/token";
}
