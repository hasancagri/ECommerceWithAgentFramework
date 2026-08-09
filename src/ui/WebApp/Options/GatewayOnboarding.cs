namespace WebApp.Options;

// E1: aday merchant descriptor + self-registration config — appsettings "GatewayOnboarding".
// GatewayOnboardingEndpoints buradan tip'li okur (istek-anindaki config[...] yerine).
public class GatewayOnboarding
{
    public string SchemaVersion { get; set; } = "1.0";
    public string Domain { get; set; } = "";
    public string LegalName { get; set; } = "";
    public string TaxId { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public string WebhookUrl { get; set; } = "";
    public string A2aCardUrl { get; set; } = "";

    // Bos ise register ucu istek origin'inden well-known descriptor URL'ini turetir.
    public string? SelfDescriptorUrl { get; set; }
}
