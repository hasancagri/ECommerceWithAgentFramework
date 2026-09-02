
namespace Basket.Api.Grpc;

// 028: checkout saga ClearBasket ucu. Ince sarici — is mantigi yok, Wolverine command'ini
// IMessageBus ile cagirir (ince gRPC sarmalayici; is mantigi slice'ta).
public class BasketClearGrpcService(IMessageBus bus) : BasketClear.BasketClearBase
{
    public override async Task<ClearBasketReply> ClearBasket(
        ClearBasketRequest request, ServerCallContext context)
    {
        var result = await bus.InvokeAsync<FeatureResultModel>(
            new ClearBasketByCheckout.ClearBasketByCheckoutCommand(
                Guid.Parse(request.UserId), Guid.Parse(request.OrderId)),
            context.CancellationToken);

        return new ClearBasketReply
        {
            Success = result.IsSuccess,
            MessageCode = result.IsSuccess ? string.Empty : result.Messages?.FirstOrDefault()?.Code ?? string.Empty
        };
    }
}