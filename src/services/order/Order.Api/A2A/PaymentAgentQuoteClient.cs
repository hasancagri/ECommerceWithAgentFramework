using System.Globalization;
using A2A;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.A2A;

namespace Order.Api.A2A;

// 070 US4: PG'nin `quote-installments` A2A skill'ini Order.Api İÇİNDEN çağırır (ChatAgent
// PaymentAgentInstallmentTool deseninin sunucu tarafı uyarlaması). PG'ye DOKUNULMAZ — taksit
// sorgusunu YALNIZ A2A sunuyor; servis-içi A2A çağrısı MCP yasağına girmez (anayasa v1.8.1) ama
// TEKNİK BORÇ (R5): PG REST quote sunarsa bu istemci düz HTTP'ye indirilir. Uzak taraf LLM
// router'dır → istekten yanıt istenen JSON biçimi açıkça dikte edilir, yanıttan dizi ayıklanır.
// Fail-open: url yok / erişilemez / çözülemez → null (çağıran dostane hata üretir; FR-012).
public sealed class PaymentAgentQuoteClient(
    IHttpClientFactory httpClientFactory,
    PaymentGatewayOption gateway,
    ILogger<PaymentAgentQuoteClient> logger) : ISingletonDependency
{
    public const string HttpClientName = "a2a-payment";

    public sealed record InstallmentOption(int InstallmentNumber, decimal TotalPrice);

    public async Task<List<InstallmentOption>?> QuoteAsync(
        Guid merchantId, string vaultToken, decimal amount, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(gateway.A2AUrl))
            return null;

        try
        {
            var httpClient = httpClientFactory.CreateClient(HttpClientName);
            var resolver = new A2ACardResolver(new Uri(gateway.A2AUrl), httpClient: httpClient);
            var agent = await resolver.GetAIAgentAsync(cancellationToken: ct);

            // PG router sözleşmesi (038): intent=installments + merchantId + vaultToken + amount.
            // Deterministik ayıklama için yanıt biçimi dikte edilir; alan adları PG tool çıktısıyla aynı.
            var prompt =
                $"intent=installments, merchantId={merchantId}, vaultToken={vaultToken}, " +
                $"amount={amount.ToString(CultureInfo.InvariantCulture)}. " +
                "Yaniti YALNIZ ham JSON dizisi olarak ver, baska hicbir metin/aciklama ekleme. Bicim: " +
                "[{\"installmentNumber\":1,\"totalPrice\":123.45}]";

            var response = await agent.RunAsync(prompt, cancellationToken: ct);
            return ParseOptions(response.Text);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "A2A quote-installments cagrisi basarisiz ({Url}).", gateway.A2AUrl);
            return null;
        }
    }

    // Uzak LLM yanıtından ilk JSON dizisini ayıklar; çözülemezse null (uydurma seçenek ASLA üretilmez).
    private List<InstallmentOption>? ParseOptions(string text)
    {
        var start = text.IndexOf('[');
        var end = text.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            logger.LogWarning("A2A quote yanitinda JSON dizisi yok: {Preview}",
                text.Length > 200 ? text[..200] : text);
            return null;
        }

        try
        {
            var options = System.Text.Json.JsonSerializer.Deserialize<List<InstallmentOption>>(
                text[start..(end + 1)],
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return options is { Count: > 0 } ? options : null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogWarning(ex, "A2A quote yaniti cozulemedi.");
            return null;
        }
    }
}