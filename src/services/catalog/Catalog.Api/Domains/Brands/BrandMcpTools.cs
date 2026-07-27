using Agent = Catalog.Api.Domains.Brands.Features.Agent;

namespace Catalog.Api.Domains.Brands;

// 016: ingestion BrandWrite adımının TEK yazma tool'u. İnce sarmalayıcıdır; get-or-create
// kararı LLM'de değil, UpsertBrand slice'ının deterministik kodundadır (R10).
[McpServerToolType]
public static class UpsertBrandMcpTool
{
    [McpServerTool(Name = "upsert_brand")]
    [Description("Markayi ada gore olusturur veya mevcut kayda baglar (get-or-create); brandId ve islemi (created/existing) doner.")]
    public static Task<FeatureObjectResultModel<Agent.UpsertBrand.UpsertBrandResponse>> UpsertBrandAsync(
        [Description("Marka adi (feed'deki yazim)")] string name,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.UpsertBrand.UpsertBrandResponse>>(
            new Agent.UpsertBrand.UpsertBrandCommand(name), ct);
}