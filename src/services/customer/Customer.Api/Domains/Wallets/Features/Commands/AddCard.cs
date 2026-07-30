namespace Customer.Api.Domains.Wallets.Features.Commands;

public static class AddCard
{
    // Ham PAN/CVV YALNIZ bu komutta (in-proc InvokeAsync; kalici kuyruga girmez). Tokenize'a
    // gecer, hicbir yere yazilmaz/loglanmaz (FR-008). Saklanan: token + gosterilebilir alanlar.
    public record AddCardCommand(
        Guid UserId,
        string Pan,
        string Cvv,
        int ExpiryMonth,
        int ExpiryYear,
        string? Label);

    public class AddCardResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class AddCardCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddCardResponse>> Handle(
            AddCardCommand cmd,
            IDocumentSession session,
            ICardTokenizer tokenizer,
            CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;

            // Tokenize'dan ONCE expiry dogrula (FR-009): gecmis kartta token uretmeyip orphan onle.
            if (!Wallet.IsExpiryInFuture(cmd.ExpiryMonth, cmd.ExpiryYear, now))
                return FeatureObjectResultModel<AddCardResponse>.Error(
                    new MessageItem { Property = nameof(cmd.ExpiryYear), Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_VALUE });

            // Gateway (stub) tokenize eder. Basarisizsa fail-closed: hicbir sey Store edilmez (FR-013).
            var token = await tokenizer.TokenizeAsync(cmd.Pan, cmd.Cvv, cmd.ExpiryMonth, cmd.ExpiryYear, ct);
            if (!token.Success)
                return FeatureObjectResultModel<AddCardResponse>.Error(
                    new MessageItem { Property = nameof(cmd.Pan), Code = token.ErrorCode ?? CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });

            var wallet = await session.Query<Wallet>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            wallet ??= Wallet.Create(cmd.UserId);

            // Yalniz token + gosterilebilir alanlar saklanir (PAN/CVV yok).
            var card = SavedCard.Create(token.Token!, token.Brand!, token.Last4!, cmd.ExpiryMonth, cmd.ExpiryYear, cmd.Label);
            var added = wallet.AddCard(card, now);
            if (!added.IsSuccess)
                return FeatureObjectResultModel<AddCardResponse>.Error(added.Messages);

            session.Store(wallet);
            return FeatureObjectResultModel<AddCardResponse>.Ok(new AddCardResponse { Id = card.Id });
        }
    }
}

public static class AddCardCommandEndpoint
{
    public record AddCardBody(string Pan, string Cvv, int ExpiryMonth, int ExpiryYear, string? Label);

    public static RouteGroupBuilder AddCardGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("", async ([FromBody] AddCardBody body, HttpContext httpContext, IMessageBus bus) =>
            {
                var userId = CurrentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AddCard.AddCardResponse>>(
                    new AddCard.AddCardCommand(userId, body.Pan, body.Cvv, body.ExpiryMonth, body.ExpiryYear, body.Label));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("AddCard")
            .RequireAuthorization(AuthorizationScopes.CustomerWrite);
        return group;
    }
}