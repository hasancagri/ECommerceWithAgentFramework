namespace Storefront.Api.Domains.StorefrontView.Ingestion;

public static class ProductChangedHandler
{
    public static async Task Handle(IntegrationEvents.ProductChangedEvent evt, IDocumentSession session, CancellationToken ct)
    {
        var existing = await session.LoadAsync<CatalogInfo>(evt.ProductId, ct);

        if (existing is null)
        {
            session.Store(CatalogInfo.Create(evt.ProductId, evt.Name, evt.ImageUrl, evt.IsDeleted, evt.OccurredAtUtc));
        }
        else if (existing.TryApply(evt.Name, evt.ImageUrl, evt.IsDeleted, evt.OccurredAtUtc))
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