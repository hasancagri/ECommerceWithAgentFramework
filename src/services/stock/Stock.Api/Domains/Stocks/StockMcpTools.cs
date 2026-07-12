using System.ComponentModel;
using ModelContextProtocol.Server;
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