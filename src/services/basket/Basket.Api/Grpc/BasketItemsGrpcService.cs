using Basket.Api.Domains.Baskets.Features.Queries;

namespace Basket.Api.Grpc;

// 039: chat siparis tamamlama — Order.Api sepet kalemlerini sunucu tarafinda okur (kalem
// sunucu-otoritesi; LLM'e girmez). Ince sarici — is mantigi yok, GetBasket query'sini IMessageBus
// ile cagirir (BasketClearGrpcService deseninin okuma muadili). Sepet yoksa bos liste + 0 total.
public class BasketItemsGrpcService(IMessageBus bus) : BasketQuery.BasketQueryBase
{
    public override async Task<GetBasketItemsReply> GetBasketItems(
        GetBasketItemsRequest request, ServerCallContext context)
    {
        var result = await bus.InvokeAsync<FeatureObjectResultModel<GetBasket.GetBasketResponse>>(
            new GetBasket.GetBasketQuery(Guid.Parse(request.UserId)), context.CancellationToken);

        var reply = new GetBasketItemsReply();
        if (!result.IsSuccess || result.Data is null)
            return reply; // sepet yok -> bos (Order tarafi 'rejected' verir)

        reply.TotalPrice = (double)result.Data.TotalPrice;
        foreach (var item in result.Data.Items)
            reply.Items.Add(new BasketLine
            {
                ProductId = item.Id.ToString(),
                Name = item.Name,
                UnitPrice = (double)item.Price,
                Quantity = item.Quantity
            });

        return reply;
    }
}
