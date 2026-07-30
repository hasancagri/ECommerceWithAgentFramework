namespace Customer.Api.Domains.Wallets.Features.Queries;

public static class GetCards
{
    [RequiredScope(AuthorizationScopes.CustomerRead)]
    public record GetCardsQuery(Guid UserId);

    // SC-002: token ve PAN/CVV HICBIR kosulda donmez — yalniz marka+son4+son-kullanma+etiket.
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

public static class GetCardsQueryEndpoint
{
    public static RouteGroupBuilder GetCardsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("", async (HttpContext httpContext, IMessageBus bus) =>
            {
                var userId = CurrentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureListResultModel<GetCards.CardView>>(
                    new GetCards.GetCardsQuery(userId));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("GetCards")
            .RequireAuthorization(AuthorizationScopes.CustomerRead);
        return group;
    }
}