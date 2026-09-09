namespace Customer.Api.Onboarding;

// 070 FR-016: DropShop (PaymentGateway) Merchant.Api /mcp + Identity bağlantı config'i — section
// "DropShopOnboarding". ChatAgent'ın DropShopGatewayOption deseninin Customer.Api taşınmışı:
// onboarding sarmalayıcı tool'ları PG MCP'sine MAKİNE kimliğiyle (client_credentials) gider;
// admin kullanıcı token'ı dış realm'e ASLA taşınmaz. McpUrl boşsa tool'lar "yapılamıyor" döner.
public class DropShopOnboardingOption
{
    public string IdentityAddress { get; set; } = "";
    public string McpUrl { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    // Merchant.Api /mcp yüzeyi merchant.write ister.
    public string Scope { get; set; } = "merchant.read merchant.write";

    public string TokenEndpoint => $"{IdentityAddress.TrimEnd('/')}/connect/token";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(McpUrl);
}