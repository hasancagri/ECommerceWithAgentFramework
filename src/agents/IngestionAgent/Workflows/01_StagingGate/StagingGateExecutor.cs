namespace IngestionAgent.Workflows._01_StagingGate;

// Aşama 1 — KAPI (kayıt başına): iş kuralları StagingRecord'un metotlarında (hash kapısı,
// yazma kararı); burası yalnız yükle-sor-işle orkestrasyonu yapar. Doğrulama yok (kullanıcı
// kararı): geçersiz veri domain servislerinin kendi kurallarında reddedilir.
public sealed class StagingGateExecutor(IDocumentStore store)
    : Executor<RecordJob, RecordJob>("staging-gate")
{
    public override async ValueTask<RecordJob> HandleAsync(
        RecordJob job, IWorkflowContext context, CancellationToken cancellationToken)
    {
        var record = job.Record;

        await using var session = store.LightweightSession();
        var existing = await session.LoadAsync<StagingRecord>(record.ExternalId, cancellationToken);

        var staging = existing ?? StagingRecord.CreateFor(record);
        job.Staging = staging;

        if (staging.IsUnchanged(record))
        {
            job.Skipped = true;
            return job;
        }

        // Karar ESKİ hafızayla verilir, yeni içerik sonra işlenir; kalıcılaştırma
        // DomainWrite sonunda (kayıt başına tek yazım, nihai durumla birlikte).
        var decision = staging.DecideWrites(record);
        job.AssumedNew = decision.AssumedNew;
        job.WriteStock = decision.WriteStock;
        job.SetDiscount = decision.SetDiscount;
        job.RemoveDiscount = decision.RemoveDiscount;

        staging.Absorb(record);
        return job;
    }
}