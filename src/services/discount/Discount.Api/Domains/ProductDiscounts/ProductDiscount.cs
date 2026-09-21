namespace Discount.Api.Domains.ProductDiscounts;

// 079 materyalize read-model (aggregate DEĞİL): kampanya aktifleşince her kitap için BİR kayıt.
// PK = ProductId → kitapta TEK indirim ("varsa atla" bunu zorlar: insert-if-not-exists). FİYAT TUTMAZ —
// yalnız yüzde + pencere (etkin fiyatı tüketici kendi liste fiyatından hesaplar).
public class ProductDiscount
{
    private ProductDiscount() { }

    public Guid ProductId { get; private set; }
    public Guid CampaignId { get; private set; }
    public int Percentage { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime? EndsAt { get; private set; }

    public static ProductDiscount Create(Guid productId, Guid campaignId, int percentage,
        DateTime startsAt, DateTime? endsAt) =>
        new()
        {
            ProductId = productId,
            CampaignId = campaignId,
            Percentage = percentage,
            StartsAt = startsAt,
            EndsAt = endsAt
        };

    /// <summary>Verilen an itibarıyla pencere içinde mi (checkout canlı doğrulama + gRPC filtresi).</summary>
    public bool IsActiveAt(DateTime now) => StartsAt <= now && (EndsAt is null || now < EndsAt.Value);

    // 079 saf apply-skip çekirdeği (İLKE VI test-first): aday kitap setini "zaten indirimli" olanlara
    // göre ikiye ayırır. toApply = indirimi olmayanlar (ilk-AKTİF-kazanır); skipped = zaten indirimli
    // (kitap başına tek indirim). Handler toApply için Store eder, existing'i Marten'den okur.
    public static (List<Guid> ToApply, List<Guid> Skipped) Partition(
        IReadOnlyCollection<Guid> candidates, IReadOnlyCollection<Guid> alreadyDiscounted)
    {
        var existing = alreadyDiscounted.ToHashSet();
        var toApply = new List<Guid>();
        var skipped = new List<Guid>();
        foreach (var id in candidates.Distinct())
            (existing.Contains(id) ? skipped : toApply).Add(id);
        return (toApply, skipped);
    }
}
