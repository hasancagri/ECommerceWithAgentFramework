namespace CustomNopCommerce.Domains.ProductReviews.ValueObjects;

/// <summary>
/// Çok-kriterli yorumda tek bir kritere verilen puan (ör. Kalite=5, Fiyat=3). ReviewType'a Id ile
/// referans verir. nopCommerce ProductReviewReviewTypeMapping paritesi. ProductReview'un child'ı.
/// </summary>
public record CriteriaRating
{
    public Guid ReviewTypeId { get; private init; }
    public int Rating { get; private init; }

    private CriteriaRating() { }

    public static CriteriaRating Create(Guid reviewTypeId, int rating) =>
        new() { ReviewTypeId = reviewTypeId, Rating = rating };
}
