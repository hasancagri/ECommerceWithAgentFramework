namespace Stock.Api.Domains.Stocks.Features.Agent;

// Agent'a açık mutlak stok atama yüzü (005-supplier-ingestion). İş mantığı kopyalanmaz;
// mevcut SetStockCommand'a delege edilir (negatif adet kuralı aggregate'te yaşar).
public static class SetStock
{
    public record SetStockCommand(Guid ProductId, int Quantity);

    public class SetStockResponse
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class SetStockCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetStockResponse>> Handle(
            SetStockCommand cmd,
            IMessageBus bus,
            CancellationToken ct)
        {
            var result = await bus
                .InvokeAsync<FeatureObjectResultModel<Commands.SetStock.SetStockResponse>>(
                    new Commands.SetStock.SetStockCommand(cmd.ProductId, cmd.Quantity), ct);

            if (!result.IsSuccess)
                return FeatureObjectResultModel<SetStockResponse>.Error(result.Messages);

            return FeatureObjectResultModel<SetStockResponse>.Ok(new SetStockResponse
            {
                ProductId = result.Data!.ProductId,
                Quantity = result.Data!.Quantity
            });
        }
    }
}