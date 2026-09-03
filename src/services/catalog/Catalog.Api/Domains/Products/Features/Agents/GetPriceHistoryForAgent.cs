namespace Catalog.Api.Domains.Products.Features.Agents;

// 063: MCP okuma slice'ı — agent bir ürünün fiyat geçmişini (append-only ProductPriceChange, 058)
// getirir. Anonim (fiyat geçmişi herkese açık; catalog MCP korumasız). İzole handler (konvansiyon).
public static class GetPriceHistoryForAgent
{
    public record GetPriceHistoryQuery(Guid ProductId);

    public class PriceHistoryEntry
    {
        public decimal? OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }

    public class GetPriceHistoryQueryHandler
    {
        public async Task<FeatureListResultModel<PriceHistoryEntry>> Handle(
            GetPriceHistoryQuery query, IDocumentSession session, CancellationToken ct)
        {
            // Son 20 kayıt; kronolojik (artan) — grafik/okuma soldan sağa.
            var history = await session.Query<ProductPriceChange>()
                .Where(x => x.ProductId == query.ProductId)
                .OrderByDescending(x => x.ChangedAtUtc)
                .Take(20)
                .ToListAsync(ct);

            var items = history
                .OrderBy(x => x.ChangedAtUtc)
                .Select(x => new PriceHistoryEntry
                {
                    OldPrice = x.OldPrice,
                    NewPrice = x.NewPrice,
                    ChangedAtUtc = x.ChangedAtUtc,
                })
                .ToList();

            return FeatureListResultModel<PriceHistoryEntry>.Ok(items);
        }
    }
}