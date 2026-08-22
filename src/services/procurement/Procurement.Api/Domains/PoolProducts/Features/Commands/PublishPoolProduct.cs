namespace Procurement.Api.Domains.PoolProducts.Features.Commands;

// Barkodun yayın kararı: buy-box değerlendirilir, TryTakePublish değişim varsa yayın işaretler.
// Yalnız EKSİKSİZ kanonik yayınlanır (FR-011); değişim yoksa hiçbir event çıkmaz (SC-007).
// Pull'dan lokal durable kuyrukla gelir (commit-sonrası işlem); dış REST yüzeyi yoktur (akış sahibi kanal).
public static class PublishPoolProduct
{
    public record PublishPoolProductCommand(string Barcode);

    [Transactional]
    public class PublishPoolProductCommandHandler
    {
        public async Task Handle(
            PublishPoolProductCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            ILogger<PublishPoolProductCommandHandler> logger,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<PoolProduct>(cmd.Barcode, ct);
            if (product is null)
                return;

            var decision = product.EvaluateBuyBox();
            var publish = product.TryTakePublish(decision.Data!);
            if (!publish.IsSuccess)
                return;

            var outcome = publish.Data!;
            if (!outcome.PublishCanonical && !outcome.PublishBuyBox)
                return; // NoChange: eksik içerik (enrich bekler) veya değişimsiz tekrar

            session.Store(product);

            if (outcome.PublishCanonical)
            {
                var c = product.Canonical!;
                var buyBox = product.PublishedBuyBox!;
                await bus.PublishAsync(new IntegrationEvents.CanonicalProductUpserted(
                    product.Barcode, c.Name, c.Description, c.Brand, c.Category, c.SubCategory, c.Sku,
                    c.Dimensions?.Weight ?? 0, c.Dimensions?.Length ?? 0,
                    c.Dimensions?.Width ?? 0, c.Dimensions?.Height ?? 0,
                    buyBox.Price ?? 0, buyBox.Stock,
                    // 043: kanonik spec adları (merge + kapalı-liste enrich sonrası).
                    c.Specs.Select(s => new IntegrationEvents.ProductSpec(s.Attribute, s.Option)).ToList(),
                    // 045: varyant ailesi kodu (null = ailesiz).
                    c.FamilyCode));
                logger.LogInformation("Kanonik yayın: {Barcode} ({Name})", product.Barcode, c.Name);
            }

            if (outcome.PublishBuyBox)
            {
                var buyBox = product.PublishedBuyBox!;
                await bus.PublishAsync(new IntegrationEvents.BuyBoxChanged(
                    product.Barcode, buyBox.SupplierId, buyBox.Price ?? 0, buyBox.Stock));
                logger.LogInformation("Buy-box yayın: {Barcode} kazanan={SupplierId} fiyat={Price} stok={Stock}",
                    product.Barcode, buyBox.SupplierId, buyBox.Price, buyBox.Stock);
            }
        }
    }
}