using Customer.Api.Infrastructure.PaymentGateway;

namespace Customer.Api.Domains.Wallets.Features.Agents;

// 075 US1: hosted kart-ekleme oturumu başlatır. PG'ye tek-kullanımlık conversationId (AddCardSession.Id)
// verir → PG Checkout Form linkini (addUrl) döner. PAN mağazaya/Claude'a uğramaz; kullanıcı
// linki tarayıcıda açıp kartı sağlayıcı ekranında girer. Kayıt CompleteAddCard (callback) ile tamamlanır.
public static class StartAddCardForAgent
{
    // Hosted formun geçerli kalacağı süre (data-model: ~15 dk). Callback bu pencerede beklenir.
    public static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(15);

    [RequiredScope(AuthorizationScopes.CustomerWrite)]
    public record StartAddCardCommand(Guid UserId);

    public class StartAddCardResponse
    {
        public string AddUrl { get; set; } = default!;
        public Guid SessionId { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    [Transactional]
    public class StartAddCardCommandHandler(IDocumentSession session, IPgCardClient pg)
    {
        public async Task<FeatureObjectResultModel<StartAddCardResponse>> Handle(
            StartAddCardCommand cmd, CancellationToken ct)
        {
            var now = DateTimeOffset.UtcNow;
            var addSession = AddCardSession.Start(cmd.UserId, now);

            // PG hosted oturumu — conversationId = tahmin-edilemez session.Id (callback korelasyonu, R3).
            var result = await pg.StartAddSessionAsync(addSession.Id, ct);
            if (!result.Success || string.IsNullOrWhiteSpace(result.AddUrl))
                return FeatureObjectResultModel<StartAddCardResponse>.Error(
                    new MessageItem { Code = CustomerResourceConstants.CARD_ADD_FAILED });

            // Link üretilebildi → oturumu Pending yaz (callback bunu UserId'ye çözer).
            session.Store(addSession);

            return FeatureObjectResultModel<StartAddCardResponse>.Ok(new StartAddCardResponse
            {
                AddUrl = result.AddUrl!,
                SessionId = addSession.Id,
                ExpiresAt = now + SessionTtl
            });
        }
    }
}
