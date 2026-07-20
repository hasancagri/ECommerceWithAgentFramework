namespace Storefront.Api.Domains.StorefrontView.Ingestion;

public static class StockChangedHandler
{
    public static async Task Handle(IntegrationEvents.StockChangedEvent evt, IDocumentSession session, CancellationToken ct)
    {
        var existing = await session.LoadAsync<StockInfo>(evt.ProductId, ct);

        if (existing is null)
        {
            session.Store(StockInfo.Create(evt.ProductId, evt.IsInStock, evt.OccurredAtUtc));
        }
        else if (existing.TryApply(evt.IsInStock, evt.OccurredAtUtc))
        {
            session.Store(existing);
        }
        else
        {
            return;
        }

        await session.SaveChangesAsync(ct);
    }
}