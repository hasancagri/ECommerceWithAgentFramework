namespace IngestionAgent.Workflows;

// Adım sonuç sözleşmesi (FR-011): LLM agent'ının structured-output çıktısı. Servis zarfının
// AYNASI değildir (FR-012) — zarfı artık kod değil LLM okur; kod yalnız bu sözleşmeyi çözer.
public record WriterResult(bool IsSuccess, string? Error);

// 016 R10: zincirin ilk iki adımı — kimlikler adımlar arasında tipli sonuçlarla akar.
// Başarı BrandId'siz/CategoryId'siz OLAMAZ (sahte-başarı emniyeti); kategori ZORUNLUDUR
// (kullanıcı kararı 2026-07-27): boş kategori adımda kesilir, null geçme yolu yoktur.
public sealed record BrandWriterResult(bool IsSuccess, string? Error, Guid? BrandId)
    : WriterResult(IsSuccess, Error);

// LLM'in kategori adımı çıkışı: yalnız kendi upsert sonucu (BrandId'yi LLM bilmez, executor taşır).
public sealed record CategoryUpsertOutcome(bool IsSuccess, string? Error, Guid? CategoryId);

public sealed record CategoryWriterResult(bool IsSuccess, string? Error, Guid? BrandId, Guid? CategoryId)
    : WriterResult(IsSuccess, Error);

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