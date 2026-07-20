namespace Stock.Api;

public static class ProductCreatedHandler
{
    // Event her zaman liste tasir: CreateProduct 1 elemanli, SeedData N elemanli yayinlar.
    public static async Task Handle(IntegrationEvents.ProductCreatedEvent evt, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        if (evt.Products.Count == 0)
            return;

        var incomingIds = evt.Products.Select(p => p.ProductId).ToArray();

        // Idempotency: zaten stok kaydi olan urunleri ayikla, sadece eksikleri ekle (tek sorgu).
        var existingIds = (await session.Query<ProductStock>()
                .Where(x => incomingIds.Contains(x.ProductId))
                .Select(x => x.ProductId)
                .ToListAsync(ct))
            .ToHashSet();

        var toCreate = evt.Products
            .Where(p => !existingIds.Contains(p.ProductId))
            .Select(p => ProductStock.Create(p.ProductId, p.Quantity))
            .ToArray();

        if (toCreate.Length == 0)
            return;

        session.Store(toCreate);
        await session.SaveChangesAsync(ct);

        // 003-storefront-read-model: ilk stok kaydi acilinca da Storefront'un StockInfo'sunu besle.
        var occurredAtUtc = DateTime.UtcNow;
        foreach (var stock in toCreate)
            await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(stock.ProductId, stock.Quantity > 0, occurredAtUtc));
    }
}