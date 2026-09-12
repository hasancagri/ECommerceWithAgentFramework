using System.ComponentModel.DataAnnotations;

namespace Payment.Api.Options;

// 075: PaymentGateway (dış repo) NON-3D çekim REST istemcisi config'i — section "PaymentGatewayOption".
// PaymentGatewayClient buradan tip'li okur. sağlayıcı PG'nin İÇİNDE (FR-016); mağaza yalnız bu tabanı bilir.
// X-Api-Key per-request MerchantInformation'dan (MerchantKeyClient) çözülür — statik anahtar yok.
public class PaymentGatewayOption
{
    [Required] public string BaseUrl { get; set; } = "";
}
