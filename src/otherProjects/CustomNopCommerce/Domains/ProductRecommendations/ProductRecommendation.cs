namespace CustomNopCommerce.Domains.ProductRecommendations;

/// <summary>
/// Bir üründen diğerine yönlü öneri bağı (kaynak→hedef). nopCommerce RelatedProduct + CrossSellProduct
/// iki anemik tablosunu TEK aggregate'te birleştirir; ayrım <see cref="RecommendationType"/> ile taşınır
/// (god-entity'yi bölmenin tersi: yakın-aynı iki tabloyu anlamlı tek kavrama toplama). Kaynak/hedef ürünlere
/// Id ile referans. Kendine bağ + aynı (kaynak,hedef,tür) tekrarı invariant'tır (handler'da query ile denetlenir,
/// tek aggregate sınırı içinde görülemez). DisplayOrder yalnız Related için anlamlı; CrossSell'de 0.
/// </summary>
public class ProductRecommendation : AggregateRoot
{
    public Guid SourceProductId { get; private set; }
    public Guid TargetProductId { get; private set; }
    public RecommendationType Type { get; private set; }
    public int DisplayOrder { get; private set; }

    private ProductRecommendation() { }

    /// <summary>Yeni öneri bağı oluşturur. Kendine-bağ + tekrar denetimi handler'da (query gerekir).</summary>
    /// <remarks>Handler: AddRecommendationCommandHandler</remarks>
    public static ProductRecommendation Create(Guid sourceProductId, Guid targetProductId,
        RecommendationType type, int displayOrder)
    {
        return new ProductRecommendation
        {
            SourceProductId = sourceProductId,
            TargetProductId = targetProductId,
            Type = type,
            DisplayOrder = displayOrder,
        };
    }

    /// <summary>Sunum sırasını değiştirir (Related listesi için).</summary>
    /// <remarks>Handler: (ileride ReorderRecommendation)</remarks>
    public ResultDomain Reorder(int displayOrder)
    {
        DisplayOrder = displayOrder;
        return ResultDomain.Ok();
    }
}
