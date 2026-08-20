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
