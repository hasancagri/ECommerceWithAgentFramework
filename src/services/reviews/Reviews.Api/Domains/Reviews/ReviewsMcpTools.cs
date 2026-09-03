using System.ComponentModel;
using Common;
using Common.Results;
using ModelContextProtocol.Server;
using Reviews.Api.Domains.Reviews.Features.Agents;

namespace Reviews.Api.Domains.Reviews;

// 064: yorum MCP yüzeyi (dış agent). get_reviews login yeter; eligibility/submit reviews.write
// (agent slice'ta [RequiredScope], Wolverine middleware zorlar). userId + görünen ad token'dan,
// gövdeden DEĞİL — ham ad/PII istemci tarafından verilemez.
[McpServerToolType]
public static class GetReviewsMcpTool
{
    [McpServerTool(Name = "get_reviews")]
    [Description(
        "Bir urunun gorunur yorumlarini (maskeli ad, puan, metin, tarih) en yeni ustte listeler. " +
        "productId = search_products/get_product'tan donen urun kimligi. page opsiyonel (varsayilan 1).")]
    public static Task<FeatureListResultModel<GetProductReviewsForAgent.ReviewItem>> GetReviewsAsync(
        [Description("Urun kimligi")] Guid productId,
        IMessageBus bus,
        CancellationToken ct,
        [Description("Sayfa numarasi (varsayilan 1)")] int page = 1)
        => bus.InvokeAsync<FeatureListResultModel<GetProductReviewsForAgent.ReviewItem>>(
            new GetProductReviewsForAgent.GetProductReviewsQuery(productId, page), ct);
}

[McpServerToolType]
public static class CheckReviewEligibilityMcpTool
{
    [McpServerTool(Name = "check_review_eligibility")]
    [Description(
        "Giris yapmis kullanicinin bu urune yorum yapip yapamayacagini (satin-alma sarti + tek-yorum) " +
        "kontrol eder. productId = urun kimligi. canReview=false ise reasonCode nedeni verir.")]
    public static Task<FeatureObjectResultModel<GetReviewEligibilityForAgent.EligibilityResponse>> CheckAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid productId,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<GetReviewEligibilityForAgent.EligibilityResponse>>(
            new GetReviewEligibilityForAgent.GetReviewEligibilityQuery(userId, productId), ct);
    }
}

[McpServerToolType]
public static class SubmitReviewMcpTool
{
    [McpServerTool(Name = "submit_review")]
    [Description(
        "Giris yapmis kullanicinin satin aldigi bir urune yorum + puan birakir (urun basina tek yorum). " +
        "productId = urun kimligi; rating 1-5; text opsiyonel yorum metni. Gorunen ad kullanicinin " +
        "kimliginden gelir (maskeli saklanir). Yanittaki 'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<SubmitReviewForAgent.SubmitReviewResponse>> SubmitAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid productId,
        int rating,
        CancellationToken ct,
        [Description("Opsiyonel yorum metni")] string? text = null)
    {
        var user = currentUser.Load(http.HttpContext!.User);
        // Görünen ad "name" claim'inden; boşsa CurrentUser.Name yedek (SubmitReview endpoint paritesi).
        var displayName = http.HttpContext!.User.FindFirst("name")?.Value ?? user.Name ?? string.Empty;
        return bus.InvokeAsync<FeatureObjectResultModel<SubmitReviewForAgent.SubmitReviewResponse>>(
            new SubmitReviewForAgent.SubmitReviewCommand(user.Id, productId, rating, text, displayName), ct);
    }
}
