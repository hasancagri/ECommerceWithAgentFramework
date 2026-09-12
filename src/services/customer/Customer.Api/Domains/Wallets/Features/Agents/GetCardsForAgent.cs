using Customer.Api.Infrastructure.PaymentGateway;

namespace Customer.Api.Domains.Wallets.Features.Agents;

// 075 US2: kayıtlı kartları PG'den CANLI listeler (yerel depo yok). Wallet.PgUserHandle → PG list-cards
// → gösterilebilir izdüşüm + opak cardHandle (sil/varsayılan referansı). SC-002: PAN/CVV/token asla.
// Cache YOK (canlı doğruluk şart — FR-004/SC-003). Handle yoksa boş liste (hata değil); PG erişilemezse
// PG_UNAVAILABLE (bayat kopya gösterme).
public static class GetCardsForAgent
{
    public record GetCardsQuery(Guid UserId);

    public class CardView
    {
        public string CardHandle { get; set; } = default!;
        public string Brand { get; set; } = default!;
        public string Last4 { get; set; } = default!;
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public string? Alias { get; set; }
        public bool IsDefault { get; set; }

        public static CardView From(PgCard c, string? defaultHandle) => new()
        {
            CardHandle = c.CardHandle,
            Brand = c.Brand,
            Last4 = c.Last4,
            ExpiryMonth = c.ExpiryMonth,
            ExpiryYear = c.ExpiryYear,
            Alias = c.Alias,
            IsDefault = defaultHandle is not null && c.CardHandle == defaultHandle
        };
    }

    public class GetCardsQueryHandler(IQuerySession session, IPgCardClient pg)
    {
        public async Task<FeatureListResultModel<CardView>> Handle(GetCardsQuery query, CancellationToken ct)
        {
            var wallet = await session.Query<Wallet>()
                .FirstOrDefaultAsync(x => x.UserId == query.UserId, ct);

            // Hiç kart eklenmemiş → boş liste (hata değil).
            if (wallet?.PgUserHandle is not { } handle || string.IsNullOrWhiteSpace(handle))
                return FeatureListResultModel<CardView>.Ok(new List<CardView>());

            var cards = await pg.ListCardsAsync(handle, ct);
            if (cards is null) // PG erişilemez → getirilemiyor (bayat kopya yok)
                return FeatureListResultModel<CardView>.Error(
                    new MessageItem { Code = CustomerResourceConstants.PG_UNAVAILABLE });

            var views = cards.Select(c => CardView.From(c, wallet.DefaultCardHandle)).ToList();
            return FeatureListResultModel<CardView>.Ok(views);
        }
    }
}
