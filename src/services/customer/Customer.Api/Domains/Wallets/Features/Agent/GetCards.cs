namespace Customer.Api.Domains.Wallets.Features.Agent;

// MCP (okuma-yalniz) icin kart listeleme slice'i. list_cards tool'u bunu IMessageBus ile sarar.
// SC-002: token/PAN/CVV asla donmez — yalniz marka+son4+expiry+etiket.
public static class GetCards
{
    public record GetCardsQuery(Guid UserId);

    public class CardView
    {
        public Guid Id { get; set; }
        public string Brand { get; set; } = default!;
        public string Last4 { get; set; } = default!;
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public string? Label { get; set; }
        public bool IsDefault { get; set; }

        public static CardView From(SavedCard c) => new()
        {
            Id = c.Id,
            Brand = c.Brand,
            Last4 = c.Last4,
            ExpiryMonth = c.ExpiryMonth,
            ExpiryYear = c.ExpiryYear,
            Label = c.Label,
            IsDefault = c.IsDefault
        };
    }

    public class GetCardsQueryHandler
    {
        public async Task<FeatureListResultModel<CardView>> Handle(
            GetCardsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var wallet = await session.Query<Wallet>()
                .FirstOrDefaultAsync(x => x.UserId == query.UserId, ct);

            var views = wallet?.Cards.Select(CardView.From).ToList() ?? new List<CardView>();
            return FeatureListResultModel<CardView>.Ok(views);
        }
    }
}