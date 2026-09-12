using Customer.Api.Infrastructure.PaymentGateway;

namespace Customer.Api.Domains.Wallets.Features.Agents;

// 075 US4: bir kartı varsayılan yapar. cardHandle kullanıcının PG listesinde doğrulanır (sahiplik) →
// Wallet.SetDefaultCard (≤1 varsayılan invariant, öncekini ezer). Varsayılan tercihi mağazada tutulur
// (PG'de varsayılan kavramı yok — R5); PAN yok.
public static class SetDefaultCardForAgent
{
    [RequiredScope(AuthorizationScopes.CustomerWrite)]
    public record SetDefaultCardCommand(Guid UserId, string CardHandle);

    public class SetDefaultCardResponse
    {
        public bool IsDefault { get; set; }
    }

    [Transactional]
    public class SetDefaultCardCommandHandler(IDocumentSession session, IPgCardClient pg)
    {
        public async Task<FeatureObjectResultModel<SetDefaultCardResponse>> Handle(
            SetDefaultCardCommand cmd, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.CardHandle))
                return FeatureObjectResultModel<SetDefaultCardResponse>.Error(
                    new MessageItem { Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

            var wallet = await session.Query<Wallet>().FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (wallet?.PgUserHandle is not { } handle || string.IsNullOrWhiteSpace(handle))
                return FeatureObjectResultModel<SetDefaultCardResponse>.Error(
                    new MessageItem { Code = CustomerResourceConstants.CARD_NOT_FOUND });

            // Sahiplik: cardHandle kullanıcının PG kart kümesinde olmalı (fail-closed).
            var cards = await pg.ListCardsAsync(handle, ct);
            if (cards is null)
                return FeatureObjectResultModel<SetDefaultCardResponse>.Error(
                    new MessageItem { Code = CustomerResourceConstants.PG_UNAVAILABLE });
            if (cards.All(c => c.CardHandle != cmd.CardHandle))
                return FeatureObjectResultModel<SetDefaultCardResponse>.Error(
                    new MessageItem { Code = CustomerResourceConstants.CARD_NOT_FOUND });

            var result = wallet.SetDefaultCard(cmd.CardHandle);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<SetDefaultCardResponse>.Error(result.Messages);

            session.Store(wallet);
            return FeatureObjectResultModel<SetDefaultCardResponse>.Ok(new SetDefaultCardResponse { IsDefault = true });
        }
    }
}
