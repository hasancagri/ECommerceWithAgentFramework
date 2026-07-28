namespace Storefront.Api;

public static class StorefrontEventHandlers
{
    public static async Task Handle(IntegrationEvents.DiscountChangedEvent evt, IDocumentSession session, CancellationToken ct)
    {
        var view = await session.LoadAsync<StorefrontView>(evt.ProductId, ct)
                   ?? StorefrontView.Create(evt.ProductId);

        view.ApplyDiscount(evt.Rate);

        session.Store(view);
        await session.SaveChangesAsync(ct);
    }
    
    public static async Task Handle(IntegrationEvents.ProductChangedEvent evt, IDocumentSession session, CancellationToken ct)
    {
        var view = await session.LoadAsync<StorefrontView>(evt.ProductId, ct)
                   ?? StorefrontView.Create(evt.ProductId);

        view.ApplyCatalog(evt.Name, evt.Description, evt.Price,
            evt.BrandId, evt.Brand, evt.CategoryId, evt.Category, evt.ImageUrl, evt.IsDeleted);

        session.Store(view);
        await session.SaveChangesAsync(ct);
    }
    
    public static async Task Handle(IntegrationEvents.StockChangedEvent evt, IDocumentSession session, CancellationToken ct)
    {
        var view = await session.LoadAsync<StorefrontView>(evt.ProductId, ct)
                   ?? StorefrontView.Create(evt.ProductId);

        view.ApplyStock(evt.Quantity);

        session.Store(view);
        await session.SaveChangesAsync(ct);
    }
}