using Agent = Stock.Api.Domains.Stocks.Features.Agent;

namespace Stock.Api.Domains.Stocks;

[McpServerToolType]
public static class GetStockMcpTool
{
    [McpServerTool(Name = "get_stock")]
    [Description("Bir urunun stok durumunu (adet) doner; urun Id'si ile sorgular.")]
    public static Task<FeatureObjectResultModel<Agent.GetStockByProductId.GetStockResponse>> GetStockAsync(
        [Description("Stok durumu sorgulanacak urunun Id'si")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.GetStockByProductId.GetStockResponse>>(
            new Agent.GetStockByProductId.GetStockByProductIdQuery(productId), ct);
}
// 005-supplier-ingestion: ingestion stok yazicisinin tool'u; Features/Agent slice'ini sarar (FR-019).
[McpServerToolType]
public static class SetStockMcpTool
{
    [McpServerTool(Name = "set_stock")]
    [Description("Bir urunun stok adedini MUTLAK degere ayarlar (arttirma/azaltma degil); negatif adet reddedilir.")]
    public static Task<FeatureObjectResultModel<Agent.SetStock.SetStockResponse>> SetStockAsync(
        [Description("Stogu ayarlanacak urunun Id'si")] Guid productId,
        [Description("Yeni mutlak stok adedi (>= 0)")] int quantity,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.SetStock.SetStockResponse>>(
            new Agent.SetStock.SetStockCommand(productId, quantity), ct);
}
