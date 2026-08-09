using System.Net.Http.Headers;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using WebApp.Options;

namespace WebApp.GatewayOnboarding;

/// <summary>
/// E1 otomatik kayıt sürüşü: DropShop Merchant.Api <c>/mcp submit_registration</c> tool'unu çağırır
/// (yapısal sonuç — A2A/LLM'den robust). 016 push-inline: başvuru alanları (domain/legalName/taxId/
/// contactEmail/webhookUrl + merchantMail) doğrudan gönderilir — descriptor URL çekme + domain-control
/// challenge YOK; tek çağrı → Pending. Token = DropShop Identity client_credentials (ecommerce-onboarding,
/// merchant.write). Config: <c>DropShopGateway:{IdentityAddress,McpUrl,ClientId,ClientSecret}</c>.
/// </summary>
public sealed class GatewayRegistrationClient(
    DropShopGatewayOption gateway, ILogger<GatewayRegistrationClient> logger)
{
    public record RegisterResult(string Status, Guid? RequestId, string? Message);

    public async Task<RegisterResult> RegisterAsync(WebApp.Options.GatewayOnboarding site, CancellationToken ct)
    {
        var mcp = await CreateMcpClientAsync(ct);
        var dto = await CallSubmitAsync(mcp, site, ct);
        return new RegisterResult(dto.Status, dto.RequestId, dto.Message);
    }

    private async Task<SubmitDto> CallSubmitAsync(
        McpClient mcp, WebApp.Options.GatewayOnboarding site, CancellationToken ct)
    {
        var result = await mcp.CallToolAsync("submit_registration",
            new Dictionary<string, object?>
            {
                ["domain"] = site.Domain,
                ["legalName"] = site.LegalName,
                ["taxId"] = site.TaxId,
                ["contactEmail"] = site.ContactEmail,
                ["webhookUrl"] = site.WebhookUrl,
                ["merchantMail"] = site.ContactEmail
            },
            cancellationToken: ct);

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(text))
            return new SubmitDto { Status = "Error", Message = "Boş MCP yanıtı." };

        try
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var env = JsonSerializer.Deserialize<EnvelopeDto>(text, opts);
            if (env is { IsSuccess: true, Data: not null })
                return env.Data;

            var code = env?.Messages?.FirstOrDefault()?.Code ?? "Bilinmeyen hata";
            return new SubmitDto { Status = "Error", Message = code };
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "submit_registration yanıtı ayrıştırılamadı: {Text}", text);
            return new SubmitDto { Status = "Error", Message = "Yanıt ayrıştırılamadı." };
        }
    }

    private async Task<McpClient> CreateMcpClientAsync(CancellationToken ct)
    {
        var mcpUrl = gateway.McpUrl;
        var token = await GetTokenAsync(ct);

        var http = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions
                {
                    Name = "dropshop-merchant-api",
                    Endpoint = new Uri(mcpUrl),
                    TransportMode = HttpTransportMode.StreamableHttp
                },
                http,
                ownsHttpClient: true),
            cancellationToken: ct);
    }

    private async Task<string> GetTokenAsync(CancellationToken ct)
    {
        var authority = gateway.IdentityAddress;
        using var http = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        using var resp = await http.PostAsync($"{authority}/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = gateway.ClientId,
                ["client_secret"] = gateway.ClientSecret,
                ["scope"] = "merchant.read merchant.write"
            }), ct);
        resp.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    private sealed class EnvelopeDto
    {
        public bool IsSuccess { get; set; }
        public SubmitDto? Data { get; set; }
        public List<MessageDto>? Messages { get; set; }
    }

    private sealed class SubmitDto
    {
        public string Status { get; set; } = string.Empty;
        public Guid? RequestId { get; set; }
        public string? Message { get; set; }
    }

    private sealed class MessageDto
    {
        public string? Property { get; set; }
        public string? Code { get; set; }
    }
}
