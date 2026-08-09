using System.ComponentModel.DataAnnotations;

namespace WebApp.Options;

// DropShop gateway (Merchant.Api /mcp + Identity) bağlantı config'i — appsettings "DropShopGateway".
// GatewayRegistrationClient buradan tip'li okur (magic-string config[...] yerine).
public class DropShopGatewayOption
{
    [Required] public string IdentityAddress { get; set; } = "";
    [Required] public string McpUrl { get; set; } = "";
    [Required] public string ClientId { get; set; } = "";
    [Required] public string ClientSecret { get; set; } = "";
}
