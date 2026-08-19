namespace CustomNopCommerce.Domains.ProductReviews.ValueObjects;

/// <summary>
/// Bir müşterinin bir yoruma "faydalı mıydı" oyu. Müşteri başına tek oy (invariant ProductReview'da).
/// nopCommerce ProductReviewHelpfulness paritesi — ama Yes/No toplamları AYRI alan değil, bu oylardan
/// türetilir (bkz. ProductReview.HelpfulYesTotal). ProductReview aggregate'inin child'ı.
/// </summary>
public record HelpfulnessVote
{
    public Guid CustomerId { get; private init; }
    public bool WasHelpful { get; private init; }

    private HelpfulnessVote() { }

    public static HelpfulnessVote Create(Guid customerId, bool wasHelpful) =>
        new() { CustomerId = customerId, WasHelpful = wasHelpful };
}
