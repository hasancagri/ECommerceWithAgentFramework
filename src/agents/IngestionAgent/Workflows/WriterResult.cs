namespace IngestionAgent.Workflows;

// Adım sonuç sözleşmesi (FR-011): LLM agent'ının structured-output çıktısı. Servis zarfının
// AYNASI değildir (FR-012) — zarfı artık kod değil LLM okur; kod yalnız bu sözleşmeyi çözer.
public record WriterResult(bool IsSuccess, string? Error);

// Catalog varyantı (FR-006): başarıda Catalog'un ürettiği ProductId'yi taşır. IsSuccess=true iken
// ProductId boşsa adım BAŞARISIZ sayılır (sahte-başarı emniyeti) — doğrulama executor'dadır.
public sealed record CatalogWriterResult(bool IsSuccess, string? Error, Guid? ProductId)
    : WriterResult(IsSuccess, Error);
    
public sealed record StockWriterResult(bool IsSuccess, string? Error, Guid? ProductId)
    : WriterResult(IsSuccess, Error);
    
public sealed record DiscountWriterResult(bool IsSuccess, string? Error, Guid? ProductId)
    : WriterResult(IsSuccess, Error);

public static class Failures
{
    public static string Describe(string code, string? detail)
        => string.IsNullOrWhiteSpace(detail) ? code : $"{code}: {detail}";
}