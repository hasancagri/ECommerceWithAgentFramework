using System.Net;
using System.Text.Json;
using WebApp.Dto;
using WebApp.Extensions;
using WebApp.Services.Refit;
using WebApp.ViewModel;

namespace WebApp.Services;

// 044: yorum listesi + form uygunlugu + gonderim. Liste anonim; eligibility/submit login'li
// kullanicinin token'iyla gider (AuthenticatedHttpClientHandler).
public class ReviewsService(
    IReviewsRefitService reviewsRefitService,
    ILogger<ReviewsService> logger)
{
    // Hata kodu → kullanici mesaji (Reviews API FeatureObjectResultModel zarfi).
    private static readonly IReadOnlyDictionary<string, string> ErrorMessages =
        new Dictionary<string, string>
        {
            ["REVIEW_PURCHASE_REQUIRED"] = "Yalnızca bu ürünü satın alanlar yorum yapabilir.",
            ["REVIEW_ALREADY_EXISTS"] = "Bu ürüne zaten bir yorumunuz var.",
            ["REVIEW_PURCHASE_CHECK_UNAVAILABLE"] = "Yorum şu an gönderilemiyor; lütfen daha sonra tekrar deneyin.",
            ["REVIEW_RATING_INVALID"] = "Puan 1-5 arası tam sayı olmalı.",
            ["REVIEW_TEXT_TOO_LONG"] = "Yorum metni en fazla 2000 karakter olabilir.",
        };

    public async Task<ReviewListViewModel> GetProductReviewsAsync(Guid productId, int page)
    {
        var response = await reviewsRefitService.GetProductReviews(productId, page, 10);

        // Bos liste API'de NotFound(400) doner — "henuz yorum yok" durumu (bos model).
        if (response.StatusCode == HttpStatusCode.BadRequest)
            return ReviewListViewModel.Empty;

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return ReviewListViewModel.Empty;
        }

        var content = response.Content!;
        return new ReviewListViewModel(
            content.Data
                .Select(r => new ReviewItemViewModel(r.MaskedName, r.Rating, r.Text, r.CreatedTime))
                .ToList(),
            content.PageNumber, content.PageCount, content.TotalItemCount);
    }

    // Form goster/gizle ongorusu; hata/erisilememe = form gizli (fail-closed ile tutarli).
    public async Task<bool> CanReviewAsync(Guid productId)
    {
        var response = await reviewsRefitService.GetEligibility(productId);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return false;
        }

        return response.Content?.Data?.CanReview ?? false;
    }

    // null = basari; dolu deger = kullaniciya gosterilecek hata metni.
    public async Task<string?> SubmitReviewAsync(Guid productId, int rating, string? text)
    {
        var response = await reviewsRefitService.SubmitReview(
            new SubmitReviewRequestDto(productId, rating, string.IsNullOrWhiteSpace(text) ? null : text.Trim()));

        if (response.IsSuccessStatusCode)
            return null;

        var code = ExtractFirstCode(response.Error?.Content);
        if (code is not null && ErrorMessages.TryGetValue(code, out var message))
            return message;

        logger.LogProblemDetails(response.Error);
        return "Yorum gönderilemedi; lütfen daha sonra tekrar deneyin.";
    }

    // FeatureObjectResultModel zarfindan ilk messages[].code degerini ceker (camelCase STJ).
    private static string? ExtractFirstCode(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("code", out var code))
                return code.GetString();
        }
        catch (JsonException)
        {
            // zarf disi govde (ProblemDetails vb.) — genel mesaja duser
        }

        return null;
    }
}
