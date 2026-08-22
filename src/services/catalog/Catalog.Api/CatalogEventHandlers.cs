namespace Catalog.Api;

// 041: Procurement yayınlarının tüketicisi. Kuyruk catalog.procurement-events Sequential işlenir
// (aynı barkodun event'leri sıralı). Kanonik kategori adı seed'li ağaçtan NormalizedName ile çözülür;
// çözülemezse exception → retry → error queue (seed hizasızlığı BUG'dır, veri durumu değil — R3).
[Transactional]
public class CatalogEventHandlers
{
    public async Task Handle(
        IntegrationEvents.CanonicalProductUpserted evt,
        IDocumentSession session,
        IMessageBus bus,
        ILogger<CatalogEventHandlers> logger,
        CancellationToken ct)
    {
        // Marka: get-or-create (016 düzeni — doğum yalnız feed'den).
        var brandNormalized = NameNormalization.Normalize(evt.Brand);
        var brand = await session.Query<Brand>()
            .FirstOrDefaultAsync(b => b.NormalizedName == brandNormalized, ct);
        if (brand is null)
        {
            var created = Brand.Create(evt.Brand);
            if (!created.IsSuccess)
            {
                logger.LogWarning("Markasız kanonik ürün yok sayıldı: {Barcode}", evt.Barcode);
                return;
            }

            brand = created.Data!;
            session.Store(brand);
        }

        // Kanonik alt kategori çözümü: primary atama alt kategoriye yapılır (contracts/integration-events.md).
        var subNormalized = NameNormalization.Normalize(evt.SubCategory);
        var category = await session.Query<Category>()
            .FirstOrDefaultAsync(c => c.NormalizedName == subNormalized, ct)
            ?? throw new InvalidOperationException(
                $"Kanonik kategori çözülemedi: '{evt.SubCategory}' ({evt.Barcode}) — taksonomi seed hizasızlığı");

        var price = Money.Create(evt.Price);
        if (price is null)
        {
            logger.LogWarning("Negatif fiyatlı kanonik ürün yok sayıldı: {Barcode}", evt.Barcode);
            return;
        }

        var product = await session.Query<Product>()
            .FirstOrDefaultAsync(p => p.Gtin == evt.Barcode, ct);
        var isNew = product is null;
        if (product is null)
        {
            product = Product.Create(evt.Name, evt.Sku, ProductType.Simple, price,
                evt.Description, evt.Description);
        }
        else
        {
            product.Rename(evt.Name);
            product.UpdateDescriptions(evt.Description, evt.Description);
            product.SetPrice(price);
        }

        product.SetIdentifiers(evt.Sku, gtin: evt.Barcode, manufacturerPartNumber: null);
        product.SetFamilyCode(evt.FamilyCode); // 045: null gelirse aileden çıkar
        product.SetBrand(brand.Id);

        // 040 pasif alanları bu feature ile dolar: ölçü feed'den (0 = bilinmiyor), SEO kanonikten türetilir.
        product.SetDimensions(ProductDimensions.Create(evt.Weight, evt.Length, evt.Width, evt.Height)
            ?? ProductDimensions.Empty());
        product.SetSeo(SeoMetadata.Create(evt.Name, null, evt.Description));

        // Primary atama = kanonik alt kategori; feed'de kategori değiştiyse eski atamalar düşürülür.
        foreach (var stale in product.Categories.Where(c => c.CategoryId != category.Id).ToList())
            product.RemoveFromCategory(stale.CategoryId);
        if (product.Categories.All(c => c.CategoryId != category.Id))
            product.AssignToCategory(category.Id, isFeatured: false, displayOrder: 0);

        // 043: kanonik spec adları registry'den Id'ye çözülür; bilinmeyen ad YOK SAYILIR (spec
        // opsiyonel içerik — kategori gibi exception ATILMAZ, satır spec'siz ilerler).
        var registry = await session.Query<Domains.SpecificationAttributes.SpecificationAttribute>()
            .Where(a => !a.IsDeleted).ToListAsync(ct);
        var assignments = new List<ProductSpecificationAssignment>();
        foreach (var spec in evt.Specs ?? [])
        {
            var attribute = registry.FirstOrDefault(a =>
                a.NormalizedName == NameNormalization.Normalize(spec.Attribute));
            var option = attribute?.Options.FirstOrDefault(o =>
                NameNormalization.Normalize(o.Name) == NameNormalization.Normalize(spec.Option));
            if (attribute is null || option is null)
            {
                logger.LogWarning("Bilinmeyen spec yok sayıldı: {Attribute}={Option} ({Barcode})",
                    spec.Attribute, spec.Option, evt.Barcode);
                continue;
            }

            assignments.Add(ProductSpecificationAssignment.Create(attribute.Id, option.Id));
        }

        var specsResult = product.SetSpecifications(assignments);
        if (!specsResult.IsSuccess)
            logger.LogWarning("Spec ataması reddedildi ({Barcode}): {Code}", evt.Barcode,
                specsResult.Messages.FirstOrDefault()?.Code);

        // K8: yazım yolu publish eder — kanonik ürün vitrindedir.
        product.Publish();
        session.Store(product);

        // Dış kontrat SABİT (040): decimal fiyat = Price.Amount, kategori = primary atama.
        // 043: Specs adlarla taşınır (Id event'e çıkmaz) — atamalar registry'den geri çözülür.
        await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
            product.Id, product.Name, product.FullDescription, product.Price.Amount,
            brand.Id, brand.Name, category.Id, category.Name,
            product.ImageUrl, IsDeleted: false,
            Specs: ResolveSpecNames(product, registry),
            FamilyCode: product.FamilyCode));

        // Yalnız YENİ üründe: Stock barkod eşlemesini kurar + ilk OnHand'i yazar (yarış edge'i kapanır — R4).
        if (isNew)
            await bus.PublishAsync(new IntegrationEvents.ProductLinked(evt.Barcode, product.Id, evt.Stock));
    }

    // US2: buy-box kararı fiyatı günceller. Bilinmeyen Gtin YOK SAYILIR — ilk değerler
    // CanonicalProductUpserted'da taşındığından kayıp olmaz (yarış edge'i, R4). Kazanansız durumda
    // ürün vitrinde KALIR (unpublish yok — FR-015); satın alınamazlık stok 0 ile sağlanır (Stock tarafı).
    public async Task Handle(
        IntegrationEvents.BuyBoxChanged evt,
        IDocumentSession session,
        IMessageBus bus,
        ILogger<CatalogEventHandlers> logger,
        CancellationToken ct)
    {
        var product = await session.Query<Product>()
            .FirstOrDefaultAsync(p => p.Gtin == evt.Barcode, ct);
        if (product is null)
            return;

        var price = Money.Create(evt.Price);
        if (price is null)
        {
            logger.LogWarning("Negatif buy-box fiyatı yok sayıldı: {Barcode}", evt.Barcode);
            return;
        }

        product.SetPrice(price);
        session.Store(product);

        // Fat kontrat: ProductChangedEvent marka/kategori adlarını da taşır (016 R7) — lookup burada.
        var brand = await session.LoadAsync<Brand>(product.BrandId, ct);
        var primary = product.Categories.FirstOrDefault();
        var category = primary is null ? null : await session.LoadAsync<Category>(primary.CategoryId, ct);
        if (brand is null || category is null)
        {
            logger.LogWarning("Buy-box fiyat yazıldı ama marka/kategori çözülemedi, event atlandı: {Barcode}", evt.Barcode);
            return;
        }

        // 043: fat kontrat spec adlarını da taşır — Storefront satırı eksik yazılmasın.
        var registry = await session.Query<Domains.SpecificationAttributes.SpecificationAttribute>()
            .Where(a => !a.IsDeleted).ToListAsync(ct);

        await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
            product.Id, product.Name, product.FullDescription, product.Price.Amount,
            brand.Id, brand.Name, category.Id, category.Name,
            product.ImageUrl, IsDeleted: false,
            Specs: ResolveSpecNames(product, registry),
            FamilyCode: product.FamilyCode));
    }

    // 043: atama Id'lerini kanonik adlara çevirir (event sözleşmesi = AD). Registry'de artık
    // bulunmayan atama sessizce düşer (seed değişimi — telemetri değil, kayıp kabul).
    private static List<IntegrationEvents.ProductSpec> ResolveSpecNames(
        Product product,
        IReadOnlyList<Domains.SpecificationAttributes.SpecificationAttribute> registry)
    {
        var specs = new List<IntegrationEvents.ProductSpec>();
        foreach (var assignment in product.Specifications)
        {
            var attribute = registry.FirstOrDefault(a => a.Id == assignment.AttributeId);
            var option = attribute?.Options.FirstOrDefault(o => o.Id == assignment.OptionId);
            if (attribute is not null && option is not null)
                specs.Add(new IntegrationEvents.ProductSpec(attribute.Name, option.Name));
        }

        return specs;
    }
}