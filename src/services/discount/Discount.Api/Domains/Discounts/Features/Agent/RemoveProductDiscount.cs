namespace Discount.Api.Domains.Discounts.Features.Agent;

// Agent'a açık indirim kaldırma yüzü (005-supplier-ingestion). Mevcut command'a delege eder.
public static class RemoveProductDiscount
{
    public record RemoveProductDiscountCommand(Guid ProductId);

    public class RemoveProductDiscountCommandHandler
    {
        public Task<FeatureResultModel> Handle(
            RemoveProductDiscountCommand cmd,
            IMessageBus bus,
            CancellationToken ct)
            => bus.InvokeAsync<FeatureResultModel>(
                new Commands.RemoveProductDiscount.RemoveProductDiscountCommand(cmd.ProductId), ct);
    }
}