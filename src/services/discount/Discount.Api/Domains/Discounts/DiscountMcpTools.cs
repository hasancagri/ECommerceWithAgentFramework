using System.ComponentModel;
using ModelContextProtocol.Server;
using Agent = Discount.Api.Domains.Discounts.Features.Agent;

namespace Discount.Api.Domains.Discounts;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agent slice'larini cagirir:
// agent'a acik her islem Agent klasorunde gorunur (kullanici karari, 005).
[McpServerToolType]
public static class GetDiscountByProductIdMcpTool
{
    [McpServerTool(Name = "get_discount_by_product")]
    [Description("Bir ürüne tanımlı indirim oranını (varsa) ürün Id'sine göre getirir.")]
    public static Task<FeatureObjectResultModel<GetDiscountByProductId.GetDiscountByProductIdResponse>> GetDiscountByProductIdAsync(
        [Description("İndirim durumu sorgulanacak ürünün Id'si")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetDiscountByProductId.GetDiscountByProductIdResponse>>(
            new GetDiscountByProductId.GetDiscountByProductIdQuery(productId), ct);
}

// 005-supplier-ingestion: ingestion indirim yazicisinin kullandigi tool'lar (FR-019).
[McpServerToolType]
public static class SetProductDiscountMcpTool
{
    [McpServerTool(Name = "set_product_discount")]
    [Description("Bir urune yuzde indirim tanimlar veya mevcut orani gunceller (upsert).")]
    public static Task<FeatureObjectResultModel<Agent.SetProductDiscount.SetProductDiscountResponse>> SetProductDiscountAsync(
        [Description("Indirim tanimlanacak urunun Id'si")] Guid productId,
        [Description("Indirim yuzdesi (0 < oran <= 100)")] decimal rate,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.SetProductDiscount.SetProductDiscountResponse>>(
            new Agent.SetProductDiscount.SetProductDiscountCommand(productId, rate), ct);
}

[McpServerToolType]
public static class RemoveProductDiscountMcpTool
{
    [McpServerTool(Name = "remove_product_discount")]
    [Description("Bir urunun tanimli indirimini kaldirir; indirim yoksa NotFound doner.")]
    public static Task<FeatureResultModel> RemoveProductDiscountAsync(
        [Description("Indirimi kaldirilacak urunun Id'si")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureResultModel>(
            new Agent.RemoveProductDiscount.RemoveProductDiscountCommand(productId), ct);
}