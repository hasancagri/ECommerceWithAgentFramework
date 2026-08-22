namespace WebApp.Dto;

// 044: Reviews API sözleşmesi (contracts/reviews-rest-api.md) — yalnız ihtiyaç duyulan alanlar.

// Liste satırı: ad her zaman MASKELİ gelir (ham ad hiçbir yanıtta yok).
public record ReviewItemDto(string MaskedName, int Rating, string? Text, DateTime CreatedTime);

// FeaturePagedResultModel zarfı (Storefront paged deseniyle aynı).
public record ReviewPagedDto(
    List<ReviewItemDto> Data,
    int TotalItemCount,
    int PageNumber,
    int PageCount,
    bool HasPreviousPage,
    bool HasNextPage);

// Form göster/gizle öngörüsü; nihai karar SubmitReview'da.
public record ReviewEligibilityDto(bool CanReview, string? ReasonCode);

// Yorum gönderme gövdesi (ad GÖNDERİLMEZ — sunucu token claim'inden alır).
public record SubmitReviewRequestDto(Guid ProductId, int Rating, string? Text);
