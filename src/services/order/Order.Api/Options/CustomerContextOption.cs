namespace Order.Api.Options;

// 039: Order -> Customer yapisal odeme-baglami istemcisi config'i — section "CustomerContextOption".
// BaseUrl bos ise Aspire service-discovery adi (customer-api) kullanilir; CustomerPaymentContextClient okur.
public class CustomerContextOption
{
    public string BaseUrl { get; set; } = "";
}
