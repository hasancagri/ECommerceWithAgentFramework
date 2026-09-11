namespace Ucp.Api.CatalogProjection;

/// <summary>
/// 072: UCP katalog projeksiyonunu ürün/stok integration event'lerinden push-only besler (Storefront
/// deseni; İlke I — Catalog/Stock DB'sine erişmez). ProductChanged → başlık/fiyat/yayın; StockChanged →
/// OnHand. Available = OnHand &gt; 0 VE !IsDeleted (her iki event yeniden hesaplar). Tek sıralı kuyruk
/// (ucp.catalog-events) → aynı satıra eşzamanlı yazım yok.
/// </summary>
public class UcpCatalogEventHandlers
{
    public async Task Handle(IntegrationEvents.ProductChangedEvent e, IDocumentSession session, CancellationToken ct)
    {
        var id = e.ProductId.ToString();
        var item = await session.LoadAsync<UcpCatalogItem>(id, ct) ?? new UcpCatalogItem { Id = id };

        item.Title = e.Name;
        item.Price = e.Price;
        item.IsDeleted = e.IsDeleted;
        var authors = e.Authors is { Count: > 0 } ? " " + string.Join(" ", e.Authors.Select(a => a.Name)) : "";
        item.SearchText = (e.Name + authors).ToLowerInvariant();
        item.Available = item.OnHand > 0 && !item.IsDeleted;

        session.Store(item);
    }

    public async Task Handle(IntegrationEvents.StockChangedEvent e, IDocumentSession session, CancellationToken ct)
    {
        var id = e.ProductId.ToString();
        var item = await session.LoadAsync<UcpCatalogItem>(id, ct) ?? new UcpCatalogItem { Id = id, Title = "", SearchText = "" };

        item.OnHand = e.Quantity;
        item.Available = item.OnHand > 0 && !item.IsDeleted;

        session.Store(item);
    }
}