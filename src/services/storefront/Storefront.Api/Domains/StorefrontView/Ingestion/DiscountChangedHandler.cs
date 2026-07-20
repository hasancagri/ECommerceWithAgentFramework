namespace Storefront.Api.Domains.StorefrontView.Ingestion;

public static class DiscountChangedHandler
{
    public static async Task Handle(IntegrationEvents.DiscountChangedEvent evt, IDocumentSession session, CancellationToken ct)
    {
        var existing = await session.LoadAsync<DiscountInfo>(evt.ProductId, ct);

        if (existing is null)
        {
            session.Store(DiscountInfo.Create(evt.ProductId, evt.Rate, evt.OccurredAtUtc));
        }
        else if (existing.TryApply(evt.Rate, evt.OccurredAtUtc))
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