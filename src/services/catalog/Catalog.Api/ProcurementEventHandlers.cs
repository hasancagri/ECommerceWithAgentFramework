namespace Catalog.Api;

// 041: Procurement yayınlarının tüketicisi. Kuyruk catalog.procurement-events Sequential işlenir
// (aynı barkodun event'leri sıralı). Kanonik kategori adı seed'li ağaçtan NormalizedName ile çözülür;
// çözülemezse exception → retry → error queue (seed hizasızlığı BUG'dır, veri durumu değil — R3).
[Transactional]
public class ProcurementEventHandlers
{
    public async Task Handle(
        IntegrationEvents.CanonicalProductUpserted evt,
        IDocumentSession session,
        IMessageBus bus,
        ILogger<ProcurementEventHandlers> logger,
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

        // K8: yazım yolu publish eder — kanonik ürün vitrindedir.
        product.Publish();
        session.Store(product);

        // Dış kontrat SABİT (040): decimal fiyat = Price.Amount, kategori = primary atama.
        await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
            product.Id, product.Name, product.FullDescription, product.Price.Amount,
            brand.Id, brand.Name, category.Id, category.Name,
            product.ImageUrl, IsDeleted: false));

        // Yalnız YENİ üründe: Stock barkod eşlemesini kurar + ilk OnHand'i yazar (yarış edge'i kapanır — R4).
        if (isNew)
            await bus.PublishAsync(new IntegrationEvents.ProductLinked(evt.Barcode, product.Id, evt.Stock));
    }
}