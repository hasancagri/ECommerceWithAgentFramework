namespace Procurement.Api.Domains.PoolProducts.Features.Commands;

// Barkodun yayın kararı (047: tek kanal). TryTakePublish değişim (içerik/fiyat/stok) varsa yayın işaretler.
// Yalnız EKSİKSİZ kanonik yayınlanır (FR-011); değişim yoksa hiçbir event çıkmaz (SC-008). Buy-box yok.
// Pull/enrich'ten lokal durable kuyrukla gelir (commit-sonrası işlem); dış REST yüzeyi yoktur.
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

            var publish = product.TryTakePublish();
            if (!publish.IsSuccess || !publish.Data!.PublishCanonical)
                return; // NoChange: eksik içerik (enrich bekler) veya değişimsiz tekrar

            session.Store(product);

            var c = product.Canonical!;
            var offer = product.CurrentOffer;
            await bus.PublishAsync(new IntegrationEvents.CanonicalProductUpserted(
                product.Barcode, c.Name, c.Description, c.Brand, c.Category, c.SubCategory, c.Sku,
                c.Dimensions?.Weight ?? 0, c.Dimensions?.Length ?? 0,
                c.Dimensions?.Width ?? 0, c.Dimensions?.Height ?? 0,
                offer.Price, offer.Stock,
                // 043: kanonik spec adları (merge + kapalı-liste enrich sonrası).
                c.Specs.Select(s => new IntegrationEvents.ProductSpec(s.Attribute, s.Option)).ToList(),
                // 045: varyant ailesi kodu (null = ailesiz).
                c.FamilyCode));
            logger.LogInformation("Kanonik yayın: {Barcode} ({Name}) fiyat={Price} stok={Stock}",
                product.Barcode, c.Name, offer.Price, offer.Stock);
        }
    }
}
