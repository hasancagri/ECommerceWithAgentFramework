namespace ChatAgent.Options;

// 032: DropShop ödeme gateway'i (Merchant.Api /mcp + Identity) bağlantı config'i — appsettings
// "DropShopGateway". ChatAgent admin persona onboarding tool'larını (submit_registration +
// registration_status) bu MCP'den MAKİNE kimliğiyle (client_credentials) toplar; kullanıcı token'ı
// gateway'e gitmez. Tek config kaynağı (IdentityServerSettings deseni; magic-string config[...] yerine).
public class DropShopGatewayOption
{
    public string IdentityAddress { get; set; } = "";
    public string McpUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    // Onboarding çağrıları için istenen scope (Merchant.Api /mcp yüzeyi merchant.write ister).
    public string Scope { get; set; } = "merchant.read merchant.write";

    // Token endpoint IdentityAddress'ten türetilir; ayrıca yazmaya gerek yok.
    public string TokenEndpoint => $"{IdentityAddress.TrimEnd('/')}/connect/token";
}