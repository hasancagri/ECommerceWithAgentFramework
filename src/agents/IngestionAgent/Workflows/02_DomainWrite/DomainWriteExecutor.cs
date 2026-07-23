using IngestionAgent.Workflows._01_StagingGate;
using IngestionAgent.Workflows._02_DomainWrite.Agents;

namespace IngestionAgent.Workflows._02_DomainWrite;

// Aşama 2 — domain'e yazım (kayıt başına): her yazıcı agent kendi MCP'sine bağlı (FR-016), LLM yok.
// Sıra: upsert_product → (gerekliyse) set_stock → (gerekliyse) set/remove_product_discount.
// Gate Failure/Skipped dediyse kayda DOKUNMAZ. Nihai durum kayıt başına tek yazımla gider.
// 007 geçiş notu: RecordJob burada ESKİ tip (_01_StagingGate); tam ad, Workflows.RecordJob
// (yeni, state'siz) ile çakışmayı önler. Bu dosya US4'te eski akışla birlikte silinir.
public sealed class DomainWriteExecutor(
    IDocumentStore store,
    CatalogWriterAgent catalogAgent,
    StockWriterAgent stockAgent,
    DiscountWriterAgent discountAgent)
    : Executor<_01_StagingGate.RecordJob, _01_StagingGate.RecordJob>("domain-write")
{
    private const string CatalogWriteFailed = "CATALOG_WRITE_FAILED";
    private const string StockWriteFailed = "STOCK_WRITE_FAILED";
    private const string DiscountWriteFailed = "DISCOUNT_WRITE_FAILED";

    public override async ValueTask<_01_StagingGate.RecordJob> HandleAsync(
        _01_StagingGate.RecordJob job, IWorkflowContext context, CancellationToken cancellationToken)
    {
        if (job.Failure is not null || job.Skipped)
            return job; // kapıda elenmiş kayıt: yazma yolu hiç açılmaz

        try
        {
            await WriteRecordAsync(job, cancellationToken);
        }
        catch (Exception ex)
        {
            job.Failure = ex.Message;
        }

        await PersistAsync(job, cancellationToken);
        return job;
    }

    private async Task WriteRecordAsync(_01_StagingGate.RecordJob job, CancellationToken ct)
    {
        var record = job.Record;
        var staging = job.Staging!;

        // 1) Katalog: SKU-anahtarlı upsert (create/update kararı Catalog'un deterministik kodunda).
        var catalog = await catalogAgent.UpsertProductAsync(record, ct);

        if (!catalog.Success || catalog.ProductId is null)
        {
            job.Failure = Fail(CatalogWriteFailed, catalog.Error);
            return;
        }

        var productId = catalog.ProductId.Value;
        staging.LinkCatalogProduct(productId);
        job.CatalogAction = catalog.Action;

        // Create bekleniyordu ama SKU katalogda zaten vardı (ör. sonucu kaybolan önceki deneme):
        // stok create yolundan açılmadı → tam senkron için set_stock devreye girer.
        if (job.AssumedNew && catalog.Action != CatalogWriterAgent.Created)
            job.WriteStock = true;

        // 2) Stok: yalnız değişim tespitinde (yeni üründe ProductCreatedEvent stok açar, R8).
        if (job.WriteStock)
        {
            var stock = await stockAgent.SetStockAsync(productId, record.StockQuantity, ct);

            if (!stock.Success)
            {
                job.Failure = Fail(StockWriteFailed, stock.Error);
                return;
            }
        }

        // 3) İndirim: yüzde varsa/değiştiyse yazılır; feed'den kalktıysa kaldırılır (FR-025/FR-026).
        if (job.SetDiscount || job.RemoveDiscount)
        {
            var discount = job.SetDiscount
                ? await discountAgent.SetAsync(productId, record.DiscountPercent!.Value, ct)
                : await discountAgent.RemoveAsync(productId, ct);

            if (!discount.Success)
                job.Failure = Fail(DiscountWriteFailed, discount.Error);
        }
    }

    private static string Fail(string code, string? detail)
        => string.IsNullOrWhiteSpace(detail) ? code : $"{code}: {detail}";

    // Kaydın nihai durumu: kayıt başına tek yazım. İçerik ancak Completed'la mühürlendiği için
    // yarım kalan kayıt bir sonraki run'da yeniden işlenir (retry + idempotent yazımlar).
    private async Task PersistAsync(_01_StagingGate.RecordJob job, CancellationToken ct)
    {
        var staging = job.Staging!;
        if (job.Failure is null) staging.MarkCompleted();
        else staging.MarkFailed(job.Failure);

        await using var session = store.LightweightSession();
        session.Store(staging);
        await session.SaveChangesAsync(ct);
    }
}