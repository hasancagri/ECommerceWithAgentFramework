using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace IngestionAgent.Infrastructure;

// ChatAgent'taki PerUserMcpTool'un TOKEN'SIZ kopyası: yazma yolu anonim (FR-008), handler yok.
// Her çağrı TAZE bir MCP session açar (R4): bayat-bağlantı/Invalidate makinesi gerekmez; düşük
// hacimde handshake maliyeti LLM gecikmesinin yanında ihmal edilebilir. Ortak kütüphaneye
// çıkarma bilinçli ertelendi (2 kopya için erken — agent-constants kararıyla tutarlı).
public sealed class AnonymousMcpTool : AIFunction
{
    private readonly HttpClient _httpClient;
    private readonly string _serverName;
    private readonly string _url;
    private readonly Tool _protocolTool;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger _logger;

    public override string Name { get; }
    public override string Description { get; }
    public override JsonElement JsonSchema { get; }

    public AnonymousMcpTool(McpClientTool schema, HttpClient httpClient, string serverName, string url, ILogger logger)
    {
        Name = schema.Name;
        Description = schema.Description;
        // Clone: kaynak şema, keşif client'ı dispose edilince geçersizleşebilecek bir JsonDocument'e
        // bağlı olabilir; kopya, client ömründen bağımsız kılar (PerUserMcpTool emsali).
        JsonSchema = schema.JsonSchema.Clone();
        _protocolTool = schema.ProtocolTool;
        _serializerOptions = schema.JsonSerializerOptions;
        _httpClient = httpClient;
        _serverName = serverName;
        _url = url;
        _logger = logger;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        try
        {
            await using var client = await McpClient.CreateAsync(
                new HttpClientTransport(
                    new HttpClientTransportOptions { Name = _serverName, Endpoint = new Uri(_url) },
                    _httpClient,
                    NullLoggerFactory.Instance,
                    ownsHttpClient: false),
                cancellationToken: cancellationToken);

            // Önbellekteki Tool tanımını taze session'a bağla; ListTools'suz, native ile aynı davranış.
            var tool = new McpClientTool(client, _protocolTool, _serializerOptions);
            return await tool.InvokeAsync(arguments, cancellationToken);
        }
        catch (Exception ex)
        {
            // Hata tool sonucu olarak modele döner: model isSuccess=false + hata koduyla raporlar
            // (prompt sözleşmesi) → executor Failure yazar → retry/DLQ. Sessiz yutma yok.
            _logger.LogWarning(ex, "MCP '{Server}' tool '{Tool}' çağrısı başarısız.", _serverName, Name);
            return $"Tool '{Name}' çağrısı başarısız oldu: {ex.Message}";
        }
    }
}