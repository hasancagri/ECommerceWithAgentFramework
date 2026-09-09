namespace Order.Api.Options;

// 039: PaymentGateway (dis repo) yapisal cekim/verify REST istemcisi config'i — section "PaymentGatewayOption".
// PaymentGatewayClient buradan tip'li okur (magic-string config[...] yerine). 049: ApiKey kaldirildi —
// X-Api-Key artik MerchantInformation'dan (MerchantKeyClient) per-request cozulur, statik anahtar yok.
public class PaymentGatewayOption
{
    [Required] public string BaseUrl { get; set; } = "";

    // 070: PG A2A ucu (quote_installments). Boşsa taksit tool'u dostane "yapılamıyor" döner.
    public string? A2AUrl { get; set; }
}
