using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace IngestionAgent.Infrastructure;

// Tool'un gerçek dönüşü: FeatureObjectResultModel'den (isSuccess, data, messages) okunur.
public sealed record ToolOutcome(bool Success, Guid? ProductId, string? Error);

// Common'daki FeatureObjectResultModel'in İHTİYAÇ KADARI aynası (proje referansı bağlamadan):
// tool'un metin zarfındaki JSON doğrudan bu tipe çözülür. Id'yi upsert_product doldurur
// (UpsertProductResponse); stok/indirim tool'larının data'sı bu alanı taşımaz (null kalır).
file sealed record ToolData(Guid? Id);

file sealed record ToolMessage(string? Code);

file sealed record ToolResponse(bool IsSuccess, ToolData? Data, List<ToolMessage>? Messages);

// MCP tool çağrısı + sonuç çözümü. Çağıran biziz — "tool çağrılmadı" belirsizliği yok.
// Bağlantı + çağrı KENDİ zaman aşımıyla sarılır (Wolverine'in 60sn execution timeout'undan kısa):
// asılı istek (kapalı servisi bekleten Aspire proxy'si) dış iptalle yutulup mesaj yanlışlıkla
// başarılı ack'leniyordu (S4 bulgusu) — artık burada hataya dönüşür → retry/DLQ politikası işler.
public static class McpToolInvoker
{
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(15);

    public static async Task<ToolOutcome> CallAsync(
        this McpConnection connection, string tool, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(CallTimeout);

        McpClient? client = null;
        try
        {
            client = await connection.GetAsync(cts.Token);
            return Parse(await client.CallToolAsync(tool, args, cancellationToken: cts.Token));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            if (client is not null) connection.Invalidate(client);
            throw new TimeoutException($"MCP tool '{tool}' {CallTimeout.TotalSeconds:0}s içinde yanıt vermedi");
        }
        catch
        {
            if (client is not null) connection.Invalidate(client);
            throw;
        }
    }

    private static ToolOutcome Parse(CallToolResult result)
    {
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;

        if (result.IsError == true)
            return new ToolOutcome(false, null, text ?? "TOOL_ERROR");

        if (string.IsNullOrWhiteSpace(text))
            return new ToolOutcome(false, null, "TOOL_RESULT_EMPTY");

        ToolResponse? response;
        try
        {
            response = JsonConvert.DeserializeObject<ToolResponse>(text);
        }
        catch (JsonException)
        {
            response = null;
        }

        if (response is null)
            return new ToolOutcome(false, null, "TOOL_RESULT_UNREADABLE");

        return new ToolOutcome(
            response.IsSuccess,
            response.Data?.Id,
            response.IsSuccess ? null : response.Messages?.FirstOrDefault()?.Code ?? "TOOL_CALL_FAILED");
    }
}