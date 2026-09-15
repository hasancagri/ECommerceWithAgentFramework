namespace Customer.Api.Onboarding;

// ═══ ANAYASA SAPMASI (v1.8.1 "MCP'yi yalnız agent tüketir") — GEREKÇELİ, plan Complexity Tracking ═══
// DropShop onboarding submit/status DIŞ solution'da (PaymentGateway Merchant.Api) YALNIZ MCP olarak
// var ve PG repo'suna dokunmak yasak (kullanıcı kısıtı). Bu istemci o yüzden agent-olmayan koddan
// imperatif MCP çağrısı yapar; sapma BU sınıfa + iki sarmalayıcı slice'a hapsedilmiştir. PG bir gün
// REST sunarsa yalnız bu sınıf değişir, tool sözleşmesi (admin_submit_onboarding/_status) değişmez.
// Kimlik: makine token'ı (OnboardingGatewayTokenHandler); admin kullanıcı token'ı dış realm'e gitmez.
public sealed class MerchantOnboardingClient(
    IHttpClientFactory httpClientFactory,
    DropShopOnboardingOption option,
    ILogger<MerchantOnboardingClient> logger) : ISingletonDependency
{
    public const string HttpClientName = "pg-merchant-onboarding";

    public bool IsConfigured => option.IsConfigured;

    // PG tool'unu çağırır, yanıt metnini (FeatureObjectResultModel JSON'u) ham döner; ulaşım/protokol
    // hatasında null (çağıran dostane "şu an yapılamıyor" üretir — teknik detay sızmaz, US3-AS/PG-kapalı).
    public async Task<string?> CallAsync(string tool, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient(HttpClientName);
            await using var client = await McpClient.CreateAsync(
                new HttpClientTransport(
                    new HttpClientTransportOptions { Name = "merchant-onboarding", Endpoint = new Uri(option.McpUrl) },
                    httpClient,
                    NullLoggerFactory.Instance,
                    ownsHttpClient: false),
                cancellationToken: ct);

            var result = await client.CallToolAsync(tool, args, cancellationToken: ct);
            return result.Content?.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DropShop onboarding MCP '{Tool}' cagrisi basarisiz.", tool);
            return null;
        }
    }
}