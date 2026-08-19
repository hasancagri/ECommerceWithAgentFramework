using CustomNopCommerce.Domains.Orders.ValueObjects;

namespace CustomNopCommerce.Domains.GiftCards.Features.Commands;

/// <summary>Yeni hediye kartı çıkarma write-slice'ı. Kupon kodu üretir; kart aktif değil doğar.</summary>
public static class IssueGiftCard
{
    public record IssueGiftCardCommand(
        GiftCardType Type,
        decimal Amount,
        string? RecipientName,
        string? RecipientEmail,
        string? SenderName,
        string? SenderEmail,
        string? Message);

    public class IssueGiftCardResponse
    {
        public Guid Id { get; set; }
        public string CouponCode { get; set; } = default!;
    }

    [Transactional]
    public class IssueGiftCardCommandHandler
    {
        public async Task<FeatureObjectResultModel<IssueGiftCardResponse>> Handle(
            IssueGiftCardCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var amount = Money.Create(cmd.Amount);
            if (amount is null || amount.Amount <= 0)
                return FeatureObjectResultModel<IssueGiftCardResponse>.Error(new MessageItem
                { Property = nameof(cmd.Amount), Code = OrderingResourceConstants.GIFTCARD_AMOUNT_INVALID });

            var couponCode = Guid.NewGuid().ToString("N")[..16].ToUpperInvariant();
            var giftCard = GiftCard.Create(cmd.Type, amount, couponCode, cmd.RecipientName, cmd.RecipientEmail,
                cmd.SenderName, cmd.SenderEmail, cmd.Message);

            session.Store(giftCard);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<IssueGiftCardResponse>.Ok(
                new IssueGiftCardResponse { Id = giftCard.Id, CouponCode = giftCard.CouponCode });
        }
    }
}

public static class IssueGiftCardCommandEndpoint
{
    public static RouteGroupBuilder IssueGiftCardGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] IssueGiftCard.IssueGiftCardCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<IssueGiftCard.IssueGiftCardResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("IssueGiftCard");
        return group;
    }
}
