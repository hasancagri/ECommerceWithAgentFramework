using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace ChatAgent;

// Boot'taki anonim kesiften yakalanan tool semasini (Name/Description/JsonSchema + protokol Tool tanimi)
// tasir; asil cagriyi CAGRI ANINDA acilan, o an ki istegin token'ina bagli TAZE stateful session'a
// yonlendirir. Boylece hicbir session iki kimlik arasinda paylasilmaz => stateful sunucu "user
// mismatch" (403) uretmez. Agent Singleton oldugu icin bu tool da tek ornek; kimlik bind'i nesnede
// degil, her InvokeCoreAsync'te acilan session'da cozulur.
//
// Cagri anda: taze session + `new McpClientTool(client, protokolTool)`. SDK'nin public ctor'u
// onbellege alinmis Tool tanimini yeni bir client'a bagladigi icin CAGRI BASINA TEKRAR ListTools
// GEREKMEZ (tek roundtrip tasarrufu); sonuc sekillendirmesi de native McpClientTool.InvokeAsync ile
// birebir aynidir (ayni SDK kod yolu) => parite riski yok.
// Tasarim: docs/superpowers/specs/2026-07-08-per-user-mcp-session-design.md
public sealed class PerUserMcpTool : AIFunction
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

    public PerUserMcpTool(McpClientTool schema, HttpClient httpClient, string serverName, string url, ILogger logger)
    {
        Name = schema.Name;
        Description = schema.Description;
        // Clone: kaynak sema, kesif client'i dispose edilince gecersiz olabilecek bir JsonDocument'e
        // bagli olabilir; kopyalayarak client'in omrunden bagimsiz kiliyoruz.
        JsonSchema = schema.JsonSchema.Clone();
        // Protokol Tool tanimini ve serializer seceneklerini sakla: cagri aninda taze session'a
        // bagli bir McpClientTool'u bunlardan yeniden kurariz (ListTools'a gerek kalmadan).
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
            // Taze, kullanici-bagli stateful session: session-create istegi paylasilan HttpClient'a
            // takili TokenInjectingHandler ile o an ki istegin token'ini tasir => bind kullaniciya oturur.
            await using var client = await McpClient.CreateAsync(
                new HttpClientTransport(
                    new HttpClientTransportOptions { Name = _serverName, Endpoint = new Uri(_url) },
                    _httpClient,
                    NullLoggerFactory.Instance,
                    ownsHttpClient: false),
                cancellationToken: cancellationToken);

            // Onbellege alinmis Tool tanimini bu taze session'a bagla ve SDK'nin kendi InvokeAsync'ine
            // delege et: ListTools yapmadan, native tool ile birebir ayni davranis.
            var tool = new McpClientTool(client, _protocolTool, _serializerOptions);
            return await tool.InvokeAsync(arguments, cancellationToken);
        }
        catch (Exception ex)
        {
            // Sessiz yutma yok: modelin gorup yanitlayabilmesi icin hatayi tool sonucu olarak dondur.
            _logger.LogWarning(ex, "MCP '{Server}' tool '{Tool}' cagrisi basarisiz.", _serverName, Name);
            return $"Tool '{Name}' cagrisi basarisiz oldu: {ex.Message}";
        }
    }
}