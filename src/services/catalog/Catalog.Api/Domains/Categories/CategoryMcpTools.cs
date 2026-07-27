using Agent = Catalog.Api.Domains.Categories.Features.Agent;

namespace Catalog.Api.Domains.Categories;

// 016: ingestion CategoryWrite adımının TEK yazma tool'u. İnce sarmalayıcıdır; get-or-create
// kararı LLM'de değil, UpsertCategory slice'ının deterministik kodundadır (R10).
[McpServerToolType]
public static class UpsertCategoryMcpTool
{
    [McpServerTool(Name = "upsert_category")]
    [Description("Kategoriyi ada gore olusturur veya mevcut kayda baglar (get-or-create); categoryId ve islemi (created/existing) doner.")]
    public static Task<FeatureObjectResultModel<Agent.UpsertCategory.UpsertCategoryResponse>> UpsertCategoryAsync(
        [Description("Kategori adi (feed'deki yazim)")] string name,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.UpsertCategory.UpsertCategoryResponse>>(
            new Agent.UpsertCategory.UpsertCategoryCommand(name), ct);
}