using Procurement.Api.Infrastructure.Enrichment;
using Procurement.Api.Seeding;

namespace Procurement.Api.Domains.PoolProducts.Features.Commands;

// Eksik içerik tamamlama (US3): lokal durable kuyruk procurement.enrich üzerinden gelir — feed işleme
// hızı AI gecikmesine bağlanmaz (R5). Hata EnrichmentException'la sinyallenir → retry 10s/30s/60s →
// error queue (mesaj içeriğiyle; kalan akış sürer). Başarı yayın zincirini tetikler.
public static class EnrichPoolProduct
{
    public const string QueueName = "procurement.enrich";

    public record EnrichPoolProductCommand(string Barcode);

    [Transactional]
    public class EnrichPoolProductCommandHandler
    {
        public async Task Handle(
            EnrichPoolProductCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            EnrichmentAgent agent,
            ILogger<EnrichPoolProductCommandHandler> logger,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<PoolProduct>(cmd.Barcode, ct);
            if (product is null)
                return;

            if (!product.NeedsEnrichment)
                return; // arada feed tamamlamış — AI'ya gerek kalmadı

            if (product.HasFreshEnrichment)
            {
                // Cache (FR-009): aynı eksik-girdi için AI TEKRAR çağrılmaz; overlay zaten uygulanmış
                // ama içerik hâlâ eksikse (AI da çözememişti) tekrar denemenin anlamı yok — logla geç.
                logger.LogInformation("Enrich cache güncel ama içerik hâlâ eksik, atlandı: {Barcode}", cmd.Barcode);
                return;
            }

            var canonical = product.Canonical!;
            EnrichmentAgent.EnrichmentOutput output;
            try
            {
                var hints = product.Listings.Where(l => !l.IsDelisted)
                    .Select(l => l.RawCategoryName)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r!)
                    .Distinct()
                    .ToList();
                output = await agent.CompleteAsync(canonical.Name, canonical.Brand,
                    canonical.Description, canonical.Category, hints, ct);
            }
            catch (Exception ex)
            {
                throw new EnrichmentException(cmd.Barcode, "model çağrısı başarısız", ex);
            }

            var result = EnrichmentResult.Create(product.MergedContentHash!,
                output.Description, output.Category, output.SubCategory);
            var apply = product.ApplyEnrichment(result, CanonicalTaxonomy.Pairs);
            if (!apply.IsSuccess)
                throw new EnrichmentException(cmd.Barcode,
                    string.Join(",", apply.Messages.Select(m => m.Code))); // liste dışı kategori → retry → DLQ (US3-4)

            if (!product.Canonical!.IsComplete)
                throw new EnrichmentException(cmd.Barcode, "AI çıktısı eksikleri kapatamadı"); // retry → DLQ

            session.Store(product);
            logger.LogInformation("Enrich tamam: {Barcode} (AI yalnız eksik alanları doldurdu)", cmd.Barcode);

            // Yayın zinciri: eksiksiz kanonik + buy-box kararı commit-sonrası yayınlanır.
            await bus.PublishAsync(new PublishPoolProduct.PublishPoolProductCommand(cmd.Barcode));
        }
    }
}
