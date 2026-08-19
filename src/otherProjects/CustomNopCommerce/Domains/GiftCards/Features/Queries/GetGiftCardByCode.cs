namespace CustomNopCommerce.Domains.GiftCards.Features.Queries;

/// <summary>Kupon koduyla hediye kartını (kalan bakiyeyle) getiren read-slice'ı.</summary>
public static class GetGiftCardByCode
{
    public record GetGiftCardByCodeQuery(string CouponCode);

    public class GiftCardResponse
    {
        public Guid Id { get; set; }
        public string CouponCode { get; set; } = default!;
        public bool IsActivated { get; set; }
        public decimal InitialAmount { get; set; }
        public decimal Balance { get; set; }
        public string Currency { get; set; } = default!;
    }

    public class GetGiftCardByCodeQueryHandler
    {
        public async Task<FeatureObjectResultModel<GiftCardResponse>> Handle(
            GetGiftCardByCodeQuery query, IQuerySession session, CancellationToken ct)
        {
            var giftCard = await session.Query<GiftCard>()
                .Where(g => g.CouponCode == query.CouponCode && !g.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (giftCard is null)
                return FeatureObjectResultModel<GiftCardResponse>.NotFound();

            return FeatureObjectResultModel<GiftCardResponse>.Ok(new GiftCardResponse
            {
                Id = giftCard.Id,
                CouponCode = giftCard.CouponCode,
                IsActivated = giftCard.IsActivated,
                InitialAmount = giftCard.InitialAmount.Amount,
                Balance = giftCard.Balance().Amount,
                Currency = giftCard.InitialAmount.Currency,
            });
        }
    }
}

public static class GetGiftCardByCodeQueryEndpoint
{
    public static RouteGroupBuilder GetGiftCardByCodeGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/by-code/{code}", async (string code, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetGiftCardByCode.GiftCardResponse>>(
                    new GetGiftCardByCode.GetGiftCardByCodeQuery(code));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetGiftCardByCode");
        return group;
    }
}
