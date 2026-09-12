using Customer.Api.Infrastructure.PaymentGateway;

namespace Customer.Api.Domains.Wallets.Features.Agents;

// 075 US3: kayıtlı kartı PG'den siler (tarayıcı yok). Sahiplik doğrulanır (cardHandle kullanıcının PG
// listesinde olmalı — FR-008; başka kullanıcının handle'ı reddedilir). Silinen kart varsayılansa
// yerel varsayılan temizlenir (FR-012).
public static class DeleteCardForAgent
{
    [RequiredScope(AuthorizationScopes.CustomerWrite)]
    public record DeleteCardCommand(Guid UserId, string CardHandle);

    public class DeleteCardResponse
    {
        public bool Deleted { get; set; }
    }

    [Transactional]
    public class DeleteCardCommandHandler(IDocumentSession session, IPgCardClient pg)
    {
        public async Task<FeatureObjectResultModel<DeleteCardResponse>> Handle(
            DeleteCardCommand cmd, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.CardHandle))
                return FeatureObjectResultModel<DeleteCardResponse>.Error(
                    new MessageItem { Code = CustomerResourceConstants.VALUE_IS_REQUIRED });

            var wallet = await session.Query<Wallet>().FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (wallet?.PgUserHandle is not { } handle || string.IsNullOrWhiteSpace(handle))
                return FeatureObjectResultModel<DeleteCardResponse>.Error(
                    new MessageItem { Code = CustomerResourceConstants.CARD_NOT_FOUND });

            // Sahiplik: cardHandle kullanıcının PG kart kümesinde olmalı (FR-008 fail-closed).
            var cards = await pg.ListCardsAsync(handle, ct);
            if (cards is null)
                return FeatureObjectResultModel<DeleteCardResponse>.Error(
                    new MessageItem { Code = CustomerResourceConstants.PG_UNAVAILABLE });
            if (cards.All(c => c.CardHandle != cmd.CardHandle))
                return FeatureObjectResultModel<DeleteCardResponse>.Error(
                    new MessageItem { Code = CustomerResourceConstants.CARD_NOT_FOUND });

            var deleted = await pg.DeleteCardAsync(handle, cmd.CardHandle, ct);
            if (!deleted)
                return FeatureObjectResultModel<DeleteCardResponse>.Error(
                    new MessageItem { Code = CustomerResourceConstants.PG_UNAVAILABLE });

            // Silinen kart varsayılansa varsayılanı temizle (FR-012).
            wallet.ClearDefaultIfMatches(cmd.CardHandle);
            session.Store(wallet);

            return FeatureObjectResultModel<DeleteCardResponse>.Ok(new DeleteCardResponse { Deleted = true });
        }
    }
}
