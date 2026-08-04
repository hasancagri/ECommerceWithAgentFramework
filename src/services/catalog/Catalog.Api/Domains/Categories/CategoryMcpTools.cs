namespace Catalog.Api.Domains.Categories;

// 016: ingestion CategoryWrite adımının TEK yazma tool'u. İnce sarmalayıcıdır; get-or-create
// kararı LLM'de değil, UpsertCategory slice'ının deterministik kodundadır (R10).
[McpServerToolType]
public static class UpsertCategoryMcpTool
{
    [McpServerTool(Name = "upsert_category")]
    [Description("Kategoriyi ada gore olusturur veya mevcut kayda baglar (get-or-create); categoryId ve islemi (created/existing) doner.")]
    public static Task<FeatureObjectResultModel<UpsertCategoryForAgent.UpsertCategoryResponse>> UpsertCategoryAsync(
        [Description("Kategori adi (feed'deki yazim)")] string name,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<UpsertCategoryForAgent.UpsertCategoryResponse>>(
            new UpsertCategoryForAgent.UpsertCategoryCommand(name), ct);
}