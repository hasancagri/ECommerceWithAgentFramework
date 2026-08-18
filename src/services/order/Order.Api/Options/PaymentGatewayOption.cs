using System.ComponentModel.DataAnnotations;

namespace Order.Api.Options;

// 039: PaymentGateway (dis repo) yapisal cekim/verify REST istemcisi config'i — section "PaymentGatewayOption".
// PaymentGatewayClient buradan tip'li okur (magic-string config[...] yerine). Auth: merchant API key.
public class PaymentGatewayOption
{
    [Required] public string BaseUrl { get; set; } = "";
    [Required] public string ApiKey { get; set; } = "";
}
