namespace Payment.Api.Options;

// 075: Payment -> Customer yapısal ödeme-bağlamı istemcisi config'i — section "CustomerContextOption".
// BaseUrl boş ise Aspire service-discovery adı (customer-api) kullanılır.
public class CustomerContextOption
{
    public string BaseUrl { get; set; } = "";
}
