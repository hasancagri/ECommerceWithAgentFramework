namespace Storefront.Api;

public static class StorefrontEventHandlers
{
    public static async Task Handle(
        IntegrationEvents.ProductChangedEvent evt,
        IDocumentSession session,
        CacheInvalidator cacheInvalidator,
        CancellationToken ct)
    {
        var view = await session.LoadAsync<StorefrontView>(evt.ProductId, ct)
                   ?? StorefrontView.Create(evt.ProductId);

        view.ApplyCatalog(evt.Name, evt.Description, evt.Price,
            // 052: event yazar çiftlerini read-model'in kendi AuthorRef'ine çevir (Shared tipini saklamaz).
            evt.Authors.Select(a => new Domains.StorefrontView.AuthorRef(a.Id, a.Name)).ToList(),
            evt.PublisherId, evt.Publisher, evt.CategoryId, evt.Category, evt.ImageUrl, evt.IsDeleted,
            // 043: kanonik spec adlari satira denormalize edilir (facet + filtre + detay).
            (evt.Specs ?? []).Select(s => SpecPair.Create(s.Attribute, s.Option)).ToList(),
            // 045: varyant ailesi kodu (null = ailesiz).
            evt.FamilyCode);

        session.Store(view);
        await session.SaveChangesAsync(ct);

        // Projeksiyon-BC invalidation kuralı (CLAUDE.md): satırı yazan handler kendi cache'ini
        // boşaltır — CacheInvalidator üzerinden (yerel + backplane). Facet verisini yalnız Catalog
        // kaynaklı alanlar etkiler; StockChangedEvent facet'e girmez, orada boşaltma yok.
        await cacheInvalidator.InvalidateAsync("filters", ct);
    }

    // 044: puan ozeti — MUTLAK deger yazilir (Count=0 temizler). Satir yoksa da yaratilir
    // (kismi satir gecerli — Catalog verisi gelince dolu-satir filtresine girer).
    public static async Task Handle(IntegrationEvents.ReviewSummaryChanged evt, IDocumentSession session, CancellationToken ct)
    {
        var view = await session.LoadAsync<StorefrontView>(evt.ProductId, ct)
                   ?? StorefrontView.Create(evt.ProductId);

        view.ApplyReviewSummary(evt.Average, evt.Count);

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

    // 054: kişisel feed sinyali — tamamlanan siparişin her kalemi UserPurchase satırına döner.
    // PK = "{userId:N}:{productId:N}" → Store = idempotent upsert (tekrar teslim/tekrar alım zararsız).
    public static async Task Handle(IntegrationEvents.OrderCompleted evt, IDocumentSession session, CancellationToken ct)
    {
        foreach (var item in evt.Items)
            session.Store(Domains.UserPurchase.UserPurchase.Create(evt.UserId, item.ProductId));

        await session.SaveChangesAsync(ct);
    }
}