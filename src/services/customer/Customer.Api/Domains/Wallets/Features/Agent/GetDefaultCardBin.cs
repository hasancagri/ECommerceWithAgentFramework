namespace Customer.Api.Domains.Wallets.Features.Agent;

// 024: MCP (okuma-yalniz) — kullanicinin DEFAULT kartinin BIN'ini (ilk 6 hane) doner. Taksit
// sorgusu icin uzak A2A agent'a girdi. HASSAS DEGIL. PAN/CVV/token ASLA donmez. Default kart
// yoksa NotFound (assistant BIN'siz genel sorgu yapar veya kart eklemesi ister).
public static class GetDefaultCardBin
{
    public record GetDefaultCardBinQuery(Guid UserId);

    public class DefaultCardBinView
    {
        // Ilk 6 hane; kart BIN'siz (eski kayit) ise null.
        public string? Bin { get; set; }
        public string Brand { get; set; } = default!;
        public string Last4 { get; set; } = default!;
    }

    public class GetDefaultCardBinQueryHandler
    {
        public async Task<FeatureObjectResultModel<DefaultCardBinView>> Handle(
            GetDefaultCardBinQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var wallet = await session.Query<Wallet>()
                .FirstOrDefaultAsync(x => x.UserId == query.UserId, ct);

            var card = wallet?.Cards.FirstOrDefault(c => c.IsDefault);
            if (card is null)
                return FeatureObjectResultModel<DefaultCardBinView>.NotFound();

            return FeatureObjectResultModel<DefaultCardBinView>.Ok(new DefaultCardBinView
            {
                Bin = card.Bin,
                Brand = card.Brand,
                Last4 = card.Last4
            });
        }
    }
}