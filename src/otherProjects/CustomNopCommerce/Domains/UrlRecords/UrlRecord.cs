namespace CustomNopCommerce.Domains.UrlRecords;

/// <summary>
/// URL kaydı (slug) — Seo bounded context'inin aggregate kökü. Herhangi bir BC'deki bir varlığı (EntityName +
/// EntityId, opak) SEO-dostu bir metne (Slug) eşler. Bir varlığın birden çok kaydı olabilir ama yalnız BİRİ
/// aktiftir (aktiflik = AggregateRoot.IsActive); eskiler pasif kalır (301/302 redirect geçmişi). Bu "tek aktif"
/// + "slug tekliği" invariant'ları aggregate'ler ARASI olduğu için handler'da query ile korunur.
/// nopCommerce UrlRecord paritesi (LanguageId çıkarıldı — çokdil deferred).
/// </summary>
public class UrlRecord : AggregateRoot
{
    // Eşlenen varlık — herhangi bir BC (Product/Category/Vendor...); opak referans.
    public Guid EntityId { get; private set; }
    public string EntityName { get; private set; } = default!;
    public string Slug { get; private set; } = default!;

    private UrlRecord() { }

    /// <summary>Yeni aktif slug kaydı oluşturur. Guard'lar + eski aktifi pasifleştirme handler'da.</summary>
    /// <remarks>Handler: SetSlugCommandHandler</remarks>
    public static UrlRecord Create(Guid entityId, string entityName, string slug) =>
        new() { EntityId = entityId, EntityName = entityName, Slug = slug };

    /// <summary>Slug kaydını pasifleştirir (yeni slug atanınca eski buraya düşer — redirect kaynağı olur).</summary>
    /// <remarks>Handler: SetSlugCommandHandler</remarks>
    public ResultDomain Deactivate()
    {
        IsActive = false;
        return ResultDomain.Ok();
    }
}
