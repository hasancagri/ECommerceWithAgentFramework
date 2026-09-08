namespace Catalog.Api.Domains.Authors;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agents slice'larini cagirir.
// TUZAK: her opsiyonel parametrenin DEFAULT'u var (LLM parametre atlarsa ArgumentException olmasin).
[McpServerToolType]
public static class ListAuthorsMcpTool
{
    [McpServerTool(Name = Shared.CatalogTools.ListAuthors)]
    [Description("Magazadaki yazarlari listeler (yalniz yayinda kitabi olanlar), kitap sayisi cok olan " +
                 "once. totalCount toplam yazar sayisidir; liste kirpilmis olabilir — daraltmak icin " +
                 "search ile ada gore filtrele.")]
    public static Task<FeatureObjectResultModel<Features.Agents.ListAuthorsForAgent.ListAuthorsResponse>> ListAuthorsAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Yazar adinda gecen metin (bos = tumu)")] string? search = null,
        [Description("Sonuc sayisi; varsayilan 50, en fazla 200")] int? maxResults = null)
        => bus.InvokeAsync<FeatureObjectResultModel<Features.Agents.ListAuthorsForAgent.ListAuthorsResponse>>(
            new Features.Agents.ListAuthorsForAgent.ListAuthorsQuery(search, maxResults), ct);
}