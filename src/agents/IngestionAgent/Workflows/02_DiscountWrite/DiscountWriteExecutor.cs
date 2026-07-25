namespace IngestionAgent.Workflows._02_DiscountWrite;

// Aşama 3 — indirim (FR-016): yüzde dolu → set (upsert); boş → remove. Remove, agent yüzünde
// idempotenttir (FR-022): indirimsiz üründe etkisiz başarı — yeniden teslim zararsız kalır.
public sealed class DiscountWriteExecutor(DiscountWriterAgent discountAgent)
    : Executor<RecordJob, RecordJob>("discount-write")
{
    private const string DiscountWriteFailed = "DISCOUNT_WRITE_FAILED";
    public static bool ShouldSet(decimal? discountPercent) => discountPercent.HasValue;

    public override async ValueTask<RecordJob> HandleAsync(
        RecordJob job, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (job.Failure is not null)
        {
            job.Completed = true; // zincir sonuna ulaşıldı; Failure handler'da exception'a döner
            return job;
        }

        try
        {
            var outcome = ShouldSet(job.Message.DiscountPercent)
                ? await discountAgent.SetAsync(
                    job.ProductId!.Value, job.Message.DiscountPercent!.Value, cancellationToken)
                : await discountAgent.RemoveAsync(job.ProductId!.Value, cancellationToken);

            if (!outcome.Success)
                job.Failure = Failures.Describe(DiscountWriteFailed, outcome.Error);
        }
        catch (Exception ex)
        {
            job.Failure = Failures.Describe(DiscountWriteFailed, ex.Message);
        }

        job.Completed = true;
        return job;
    }
}