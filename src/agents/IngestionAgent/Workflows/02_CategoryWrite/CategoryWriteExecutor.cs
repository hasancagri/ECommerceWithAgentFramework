namespace IngestionAgent.Workflows._02_CategoryWrite;

// Aşama 2 — kategori upsert (016 R10): girdisi marka adımının tipli sonucu; buraya yalnız BAŞARILI
// sonuç yönlenir (conditional edge). Kategori adı ctor'daki mesajdan gelir; BrandId'yi KOD taşır.
// Kategori ZORUNLUDUR (kullanıcı kararı 2026-07-27): boş/eksik kategori LLM'e gitmeden deterministik
// hatadır → short-circuit → retry/DLQ. Başarı CategoryId'siz OLAMAZ (sahte-başarı emniyeti).
public sealed class CategoryWriteExecutor(
    CategoryWriterAgent categoryAgent, IntegrationEvents.SupplierProductSnapshotReceived message)
    : Executor<BrandWriterResult, CategoryWriterResult>("category-write")
{
    private const string CategoryWriteFailed = "CATEGORY_WRITE_FAILED";

    public override async ValueTask<CategoryWriterResult> HandleAsync(
        BrandWriterResult brandResult, IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var brandId = brandResult.BrandId!.Value; // edge yalnız başarıyı getirir; başarı Id'siz olamaz

        if (string.IsNullOrWhiteSpace(message.Category))
            return new CategoryWriterResult(false,
                Failures.Describe(CategoryWriteFailed, "CATEGORY_MISSING"), brandId, null);

        try
        {
            var result = await categoryAgent.UpsertAsync(message.Category, cancellationToken);

            if (result is { IsSuccess: true, CategoryId: not null })
                return new CategoryWriterResult(true, null, brandId, result.CategoryId);

            return new CategoryWriterResult(false,
                Failures.Describe(CategoryWriteFailed, result.IsSuccess ? "CATEGORY_ID_MISSING" : result.Error),
                brandId, null);
        }
        catch (Exception ex)
        {
            return new CategoryWriterResult(false,
                Failures.Describe(CategoryWriteFailed, ex.Message), brandId, null);
        }
    }
}