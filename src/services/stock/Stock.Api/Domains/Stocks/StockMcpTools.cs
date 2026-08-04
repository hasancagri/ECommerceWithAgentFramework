namespace Stock.Api.Domains.Stocks;

[McpServerToolType]
public static class GetStockMcpTool
{
    [McpServerTool(Name = "get_stock")]
    [Description("Bir urunun stok durumunu (adet) doner; urun Id'si ile sorgular.")]
    public static Task<FeatureObjectResultModel<GetStockByProductIdForAgent.GetStockResponse>> GetStockAsync(
        [Description("Stok durumu sorgulanacak urunun Id'si")] Guid productId,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetStockByProductIdForAgent.GetStockResponse>>(
            new GetStockByProductIdForAgent.GetStockByProductIdQuery(productId), ct);
}
// 005-supplier-ingestion: ingestion stok yazicisinin tool'u; Features/Agent slice'ini sarar (FR-019).
[McpServerToolType]
public static class SetStockMcpTool
{
    [McpServerTool(Name = "set_stock")]
    [Description("Bir urunun stok adedini MUTLAK degere ayarlar (arttirma/azaltma degil); negatif adet reddedilir.")]
    public static Task<FeatureObjectResultModel<SetStockForAgent.SetStockResponse>> SetStockAsync(
        [Description("Stogu ayarlanacak urunun Id'si")] Guid productId,
        [Description("Yeni mutlak stok adedi (>= 0)")] int quantity,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<SetStockForAgent.SetStockResponse>>(
            new SetStockForAgent.SetStockCommand(productId, quantity), ct);
}
