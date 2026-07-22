namespace Discount.Api.Domains.Discounts.Features.Agent;

// Agent'a açık indirim tanımlama yüzü (005-supplier-ingestion). İş mantığı kopyalanmaz;
// mevcut SetProductDiscountCommand'a delege edilir (yazma yolu tektir).
public static class SetProductDiscount
{
    public record SetProductDiscountCommand(Guid ProductId, decimal Rate);

    public class SetProductDiscountResponse
    {
        public Guid ProductId { get; set; }
        public decimal Rate { get; set; }
    }

    public class SetProductDiscountCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetProductDiscountResponse>> Handle(
            SetProductDiscountCommand cmd,
            IMessageBus bus,
            CancellationToken ct)
        {
            var result = await bus
                .InvokeAsync<FeatureObjectResultModel<Commands.SetProductDiscount.SetProductDiscountResponse>>(
                    new Commands.SetProductDiscount.SetProductDiscountCommand(cmd.ProductId, cmd.Rate), ct);

            if (!result.IsSuccess)
                return FeatureObjectResultModel<SetProductDiscountResponse>.Error(result.Messages);

            return FeatureObjectResultModel<SetProductDiscountResponse>.Ok(new SetProductDiscountResponse
            {
                ProductId = result.Data!.ProductId,
                Rate = result.Data!.Rate
            });
        }
    }
}