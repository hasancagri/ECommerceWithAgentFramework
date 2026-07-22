using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace IngestionAgent.Infrastructure;

// Tool'un gerçek dönüşü: FeatureObjectResultModel'den (isSuccess, data, messages) okunur.
public sealed record ToolOutcome(bool Success, Guid? ProductId, string? Action, string? Error);

// Common'daki FeatureObjectResultModel'in İHTİYAÇ KADARI aynası (proje referansı bağlamadan):
// tool'un metin zarfındaki JSON doğrudan bu tipe çözülür. Id/Action'ı upsert_product doldurur
// (UpsertProductResponse); stok/indirim tool'larının data'sı bu alanları taşımaz (null kalır).
file sealed record ToolData(Guid? Id, string? Action);

file sealed record ToolMessage(string? Code);

file sealed record ToolResponse(bool IsSuccess, ToolData? Data, List<ToolMessage>? Messages);

// MCP tool çağrısı + sonuç çözümü. Çağıran biziz — "tool çağrılmadı" belirsizliği yok.
public static class McpToolInvoker
{
    public static async Task<ToolOutcome> CallAsync(
        this McpClient client, string tool, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        var result = await client.CallToolAsync(tool, args, cancellationToken: ct);
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;

        if (result.IsError == true)
            return new ToolOutcome(false, null, null, text ?? "TOOL_ERROR");

        if (string.IsNullOrWhiteSpace(text))
            return new ToolOutcome(false, null, null, "TOOL_RESULT_EMPTY");

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
            return new ToolOutcome(false, null, null, "TOOL_RESULT_UNREADABLE");

        return new ToolOutcome(
            response.IsSuccess,
            response.Data?.Id,
            response.Data?.Action,
            response.IsSuccess ? null : response.Messages?.FirstOrDefault()?.Code ?? "TOOL_CALL_FAILED");
    }
}