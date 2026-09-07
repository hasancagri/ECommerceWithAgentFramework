namespace Catalog.Api.Domains.Publishers;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agents slice'larini cagirir.
[McpServerToolType]
public static class ListPublishersMcpTool
{
    [McpServerTool(Name = "list_publishers")]
    [Description("Magazadaki yayinevlerini listeler (yalniz yayinda kitabi olanlar), kitap sayisi cok " +
                 "olan once. totalCount toplam yayinevi sayisidir; daraltmak icin search kullan.")]
    public static Task<FeatureObjectResultModel<Features.Agents.ListPublishersForAgent.ListPublishersResponse>> ListPublishersAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Yayinevi adinda gecen metin (bos = tumu)")] string? search = null,
        [Description("Sonuc sayisi; varsayilan 100, en fazla 200")] int? maxResults = null)
        => bus.InvokeAsync<FeatureObjectResultModel<Features.Agents.ListPublishersForAgent.ListPublishersResponse>>(
            new Features.Agents.ListPublishersForAgent.ListPublishersQuery(search, maxResults), ct);
}