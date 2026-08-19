using CustomNopCommerce.Domains.ProductReviews.ValueObjects;

namespace CustomNopCommerce.Domains.ProductReviews;

/// <summary>
/// Ürün yorumu — Reviews bounded context'inin kök aggregate'i. Reviews AYRI bir BC'dir; Catalog'un
/// Product'ına DOĞRUDAN erişmez — <see cref="ProductId"/> ve <see cref="CustomerId"/> opak Id referanslarıdır
/// (gerçek mikroservis sınırı). Zengin aggregate dersleri: (1) puan 1-5 invariant'ı; (2) faydalı-oyları
/// child koleksiyon, <see cref="HelpfulYesTotal"/>/<see cref="HelpfulNoTotal"/> SAKLANMAZ, oylardan TÜRETİLİR;
/// (3) müşteri başına tek oy invariant'ı; (4) onay yaşam döngüsü (moderasyon). nopCommerce ProductReview
/// paritesi (StoreId çok-mağazaya, CustomerNotifiedOfReply Messaging'e bırakıldı).
/// </summary>
public class ProductReview : AggregateRoot
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

    public Guid ProductId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Title { get; private set; } = default!;
    public string ReviewText { get; private set; } = default!;
    public int Rating { get; private set; }
    public bool IsApproved { get; private set; }
    // Satıcı/admin cevabı; henüz yoksa null.
    public string? ReplyText { get; private set; }

    private readonly List<HelpfulnessVote> _helpfulnessVotes = new();
    public IReadOnlyList<HelpfulnessVote> HelpfulnessVotes => _helpfulnessVotes;

    private readonly List<CriteriaRating> _criteriaRatings = new();
    public IReadOnlyList<CriteriaRating> CriteriaRatings => _criteriaRatings;

    // Türetilmiş — ayrı alan tutulmaz; her zaman oylarla tutarlı (invariant otomatik).
    public int HelpfulYesTotal => _helpfulnessVotes.Count(v => v.WasHelpful);
    public int HelpfulNoTotal => _helpfulnessVotes.Count(v => !v.WasHelpful);

    private ProductReview() { }

    /// <summary>Yeni yorum oluşturur (onaysız doğar — moderasyon bekler). Başlık/metin/puan guard'ı handler'da.</summary>
    /// <remarks>Handler: CreateProductReviewCommandHandler</remarks>
    public static ProductReview Create(Guid productId, Guid customerId, string title, string reviewText, int rating)
    {
        return new ProductReview
        {
            ProductId = productId,
            CustomerId = customerId,
            Title = title,
            ReviewText = reviewText,
            Rating = rating,
            IsApproved = false,
        };
    }

    /// <summary>Yorumu onaylar (vitrinde görünür olur).</summary>
    /// <remarks>Handler: ApproveProductReviewCommandHandler</remarks>
    public ResultDomain Approve()
    {
        IsApproved = true;
        return ResultDomain.Ok();
    }

    /// <summary>Yorumun onayını kaldırır (vitrinden gizler).</summary>
    /// <remarks>Handler: (ileride UnapproveProductReview)</remarks>
    public ResultDomain Unapprove()
    {
        IsApproved = false;
        return ResultDomain.Ok();
    }

    /// <summary>Yoruma satıcı/admin cevabı ekler veya günceller.</summary>
    /// <remarks>Handler: (ileride ReplyToProductReview)</remarks>
    public ResultDomain Reply(string replyText)
    {
        ReplyText = replyText;
        return ResultDomain.Ok();
    }

    /// <summary>Faydalı-oyu ekler. Müşteri başına tek oy (invariant): aynı müşteri ikinci kez oy veremez.</summary>
    /// <remarks>Handler: VoteHelpfulnessCommandHandler</remarks>
    public ResultDomain AddHelpfulnessVote(Guid customerId, bool wasHelpful)
    {
        if (_helpfulnessVotes.Any(v => v.CustomerId == customerId))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(customerId), Code = CatalogResourceConstants.REVIEW_ALREADY_VOTED });
        _helpfulnessVotes.Add(HelpfulnessVote.Create(customerId, wasHelpful));
        return ResultDomain.Ok();
    }

    /// <summary>Bir review-type kriterine puan ekler (çok-kriterli yorum). Puan 1-5 olmalı.</summary>
    /// <remarks>Handler: (ileride AddCriteriaRating)</remarks>
    public ResultDomain AddCriteriaRating(Guid reviewTypeId, int rating)
    {
        if (rating < MinRating || rating > MaxRating)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(rating), Code = CatalogResourceConstants.REVIEW_RATING_RANGE });
        _criteriaRatings.Add(CriteriaRating.Create(reviewTypeId, rating));
        return ResultDomain.Ok();
    }
}
