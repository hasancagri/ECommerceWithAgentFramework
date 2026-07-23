namespace Discount.Api.Domains.Discounts.Features.Agent;

// Agent'a açık indirim kaldırma yüzü (005-supplier-ingestion). Mevcut command'a delege eder.
// 007/FR-022: makine tüketicisi için NotFound etkisiz başarıdır (idempotent remove);
// domain command'ı ve REST DELETE (404) davranışı DEĞİŞMEZ (R7).
public static class RemoveProductDiscount
{
    public record RemoveProductDiscountCommand(Guid ProductId);

    public class RemoveProductDiscountCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            RemoveProductDiscountCommand cmd,
            IMessageBus bus,
            CancellationToken ct)
        {
            var result = await bus.InvokeAsync<FeatureResultModel>(
                new Commands.RemoveProductDiscount.RemoveProductDiscountCommand(cmd.ProductId), ct);

            return AsIdempotent(result);
        }

        public static FeatureResultModel AsIdempotent(FeatureResultModel result)
            => IsNotFound(result) ? FeatureResultModel.Ok() : result;

        private static bool IsNotFound(FeatureResultModel result)
            => !result.IsSuccess
               && result.Messages?.Any(m => m.Code == CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND) == true;
    }
}