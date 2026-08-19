using CustomNopCommerce.Domains.Orders.ValueObjects;

namespace CustomNopCommerce.Domains.GiftCards.Features.Commands;

/// <summary>Hediye kartından tutar harcama write-slice'ı. Aktiflik + bakiye invariant'ı aggregate'te.</summary>
public static class RedeemGiftCard
{
    public record RedeemGiftCardCommand(Guid GiftCardId, decimal Amount, Guid? OrderId);

    [Transactional]
    public class RedeemGiftCardCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            RedeemGiftCardCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var giftCard = await session.LoadAsync<GiftCard>(cmd.GiftCardId, ct);
            if (giftCard is null || giftCard.IsDeleted)
                return FeatureResultModel.NotFound();

            var amount = Money.Create(cmd.Amount, giftCard.InitialAmount.Currency);
            if (amount is null)
                return FeatureResultModel.Error(new MessageItem
                { Property = nameof(cmd.Amount), Code = OrderingResourceConstants.GIFTCARD_REDEEM_AMOUNT_INVALID });

            var result = giftCard.Redeem(amount, cmd.OrderId, DateTime.UtcNow);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(giftCard);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class RedeemGiftCardCommandEndpoint
{
    public static RouteGroupBuilder RedeemGiftCardGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/redeem", async (Guid id,
            [FromBody] RedeemGiftCard.RedeemGiftCardCommand body, IMessageBus bus) =>
            {
                var cmd = body with { GiftCardId = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("RedeemGiftCard");
        return group;
    }
}
