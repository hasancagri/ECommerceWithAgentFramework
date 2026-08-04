namespace Catalog.Api.Domains.Brands;

// 016: ingestion BrandWrite adımının TEK yazma tool'u. İnce sarmalayıcıdır; get-or-create
// kararı LLM'de değil, UpsertBrand slice'ının deterministik kodundadır (R10).
[McpServerToolType]
public static class UpsertBrandMcpTool
{
    [McpServerTool(Name = "upsert_brand")]
    [Description("Markayi ada gore olusturur veya mevcut kayda baglar (get-or-create); brandId ve islemi (created/existing) doner.")]
    public static Task<FeatureObjectResultModel<UpsertBrandForAgent.UpsertBrandResponse>> UpsertBrandAsync(
        [Description("Marka adi (feed'deki yazim)")] string name,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<UpsertBrandForAgent.UpsertBrandResponse>>(
            new UpsertBrandForAgent.UpsertBrandCommand(name), ct);
}