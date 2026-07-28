namespace Storefront.Api;

public static class StorefrontEventHandlers
{
    public static async Task Handle(
        IntegrationEvents.ProductChangedEvent evt,
        IDocumentSession session,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<IntegrationEvents.ProductChangedEvent> logger,
        CancellationToken ct)
    {
        var view = await session.LoadAsync<StorefrontView>(evt.ProductId, ct)
                   ?? StorefrontView.Create(evt.ProductId);

        view.ApplyCatalog(evt.Name, evt.Description, evt.Price,
            evt.BrandId, evt.Brand, evt.CategoryId, evt.Category, evt.ImageUrl, evt.IsDeleted);

        session.Store(view);
        await session.SaveChangesAsync(ct);

        // 019: view kaydi YUKARIDA garanti altina alindi (FR-014); embedding ayri kayit + ayri
        // SaveChanges. Uretim hatasi yutulur — kayit eksik kalir, urun anlamsal siralamaya girmez,
        // sonraki ProductChangedEvent'te hash farki yeniden uretimi tetikler (FR-015).
        await RefreshEmbeddingAsync(view, session, embeddingGenerator, logger, ct);
    }

    public static async Task Handle(IntegrationEvents.StockChangedEvent evt, IDocumentSession session, CancellationToken ct)
    {
        // Stok degisimi arama metnine girmez → embedding'e BILEREK dokunulmaz (FR-013/SC-004).
        var view = await session.LoadAsync<StorefrontView>(evt.ProductId, ct)
                   ?? StorefrontView.Create(evt.ProductId);

        view.ApplyStock(evt.Quantity);

        session.Store(view);
        await session.SaveChangesAsync(ct);
    }

    private static async Task RefreshEmbeddingAsync(
        StorefrontView view,
        IDocumentSession session,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger logger,
        CancellationToken ct)
    {
        var searchText = ProductEmbedding.BuildSearchText(view.Name, view.Description, view.Brand, view.Category);
        if (searchText is null)
            return;

        var textHash = ProductEmbedding.ComputeTextHash(searchText);
        var existing = await session.LoadAsync<ProductEmbedding>(view.ProductId, ct);
        if (existing?.TextHash == textHash)
            return; // metin degismedi → uretim yok (FR-013)

        try
        {
            var vector = await embeddingGenerator.GenerateVectorAsync(searchText, cancellationToken: ct);

            if (existing is null)
                existing = ProductEmbedding.Create(view.ProductId, textHash, vector.ToArray());
            else
                existing.Refresh(textHash, vector.ToArray());

            session.Store(existing);
            await session.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Embedding uretimi basarisiz (ProductId {ProductId}); vitrin satiri etkilenmedi, sonraki degisiklikte yeniden denenecek",
                view.ProductId);
        }
    }
}