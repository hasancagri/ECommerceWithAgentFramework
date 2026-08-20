namespace Procurement.Api.Infrastructure.Enrichment;

// Enrich yolunun retry/DLQ sinyali (IngestionWriteException deseni mirası): geçici hata (model/servis
// kapalı) kademeli retry penceresinde kurtulur; ısrarcı hata error queue'ya mesaj içeriğiyle düşer.
public sealed class EnrichmentException(string barcode, string reason, Exception? inner = null)
    : Exception($"Enrich başarısız ({barcode}): {reason}", inner);
