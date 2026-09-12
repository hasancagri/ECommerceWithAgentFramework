using Customer.Api.Infrastructure.PaymentGateway;

namespace Customer.Api.Domains.Wallets.Features.Agents;

// 075 US1: hosted kart-ekleme callback'i. conversationId (AddCardSession.Id) → UserId çözülür, PG'den
// pgUserHandle alınır → Wallet.SetPgUserHandle (idempotent çapa) + ilk kartsa varsayılan yapılır
// (FR-001a). İptal/hata → kalıcı kayıt YOK (FR-010). JWT'siz callback ucundan (T012) tetiklenir →
// kimlik UserId session'dan; RequiredScope YOK (makine korelasyonu, R3). Tek-kullanımlık: tüketilen
// oturum tekrar işlenmez.
public static class CompleteAddCardForAgent
{
    public record CompleteAddCardCommand(Guid SessionId);

    [Transactional]
    public class CompleteAddCardCommandHandler(IDocumentSession session, IPgCardClient pg)
    {
        public async Task<FeatureResultModel> Handle(CompleteAddCardCommand cmd, CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            var addSession = await session.LoadAsync<AddCardSession>(cmd.SessionId, ct);
            if (addSession is null)
                return FeatureResultModel.Error(new MessageItem { Code = CustomerResourceConstants.CARD_NOT_FOUND });

            // Tek-kullanımlık + süre-sınırlı: tüketilmiş/dolmuş oturum işlenmez (R3).
            if (!addSession.IsUsable(now, StartAddCardForAgent.SessionTtl))
            {
                if (addSession.Status == AddCardSessionStatus.Pending) // süre dolmuş Pending → Expired
                {
                    addSession.Expire();
                    session.Store(addSession);
                }
                return FeatureResultModel.Error(new MessageItem { Code = CustomerResourceConstants.CARD_ADD_FAILED });
            }

            var result = await pg.CompleteAddAsync(cmd.SessionId, ct);
            if (result.Status != PgAddStatus.Success || string.IsNullOrWhiteSpace(result.PgUserHandle))
            {
                // İptal/hata → kayıt yapma; kalıcı sonuçsa oturumu kapat (Pending ise tekrar denenebilir).
                if (result.Status == PgAddStatus.Failure)
                {
                    addSession.Cancel();
                    session.Store(addSession);
                }
                return FeatureResultModel.Error(new MessageItem { Code = CustomerResourceConstants.CARD_ADD_FAILED });
            }

            var wallet = await session.Query<Wallet>().FirstOrDefaultAsync(x => x.UserId == addSession.UserId, ct)
                         ?? Wallet.Create(addSession.UserId);

            var set = wallet.SetPgUserHandle(result.PgUserHandle!);
            if (!set.IsSuccess)
                return FeatureResultModel.Error(set.Messages);

            // İlk kart otomatik varsayılan (FR-001a): varsayılan boşsa PG'deki ilk kartı işaretle.
            if (string.IsNullOrWhiteSpace(wallet.DefaultCardHandle))
            {
                var cards = await pg.ListCardsAsync(result.PgUserHandle!, ct);
                var first = cards?.FirstOrDefault();
                if (first is not null)
                    wallet.MarkFirstCardDefault(first.CardHandle);
            }

            addSession.Complete();
            session.Store(wallet);
            session.Store(addSession);
            return FeatureResultModel.Ok();
        }
    }
}
