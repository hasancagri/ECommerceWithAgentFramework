using Basket.Api.Domains.Baskets.Features.Commands;
using static Shared.CheckoutMessages;

namespace Basket.Api;

// 049: checkout orchestrator sepet temizleme broker handler'ı. Komutu BasketCommandsQueue'dan tüketir,
// mevcut ClearBasketByCheckout domain slice'ını IMessageBus ile çağırır (tek yazım yolu), sonucu reply
// kuyruğuna yayınlar. Pivot sonrası geç adım — başarısızlık siparişi iptal etmez (orchestrator retry/log).
public class BasketEventHandlers
{
    public async Task<BasketCleared> Handle(ClearBasketCommand cmd, IMessageBus bus, CancellationToken ct)
    {
        var r = await bus.InvokeAsync<FeatureResultModel>(
            new ClearBasketByCheckout.ClearBasketByCheckoutCommand(cmd.UserId, cmd.CheckoutId), ct);

        return r.IsSuccess
            ? new BasketCleared(cmd.CheckoutId, true, ErrorClass.None)
            : new BasketCleared(cmd.CheckoutId, false, ErrorClass.Transient, r.Messages?.FirstOrDefault()?.Code);
    }
}