namespace Catalog.Api.Domains.Products.Features.Agents;

// Agent'a açık TEK yazma yüzü (005): SKU-anahtarlı upsert. Create/update kararı LLM'de değil,
// burada deterministik koddadır — SKU varsa güncelle, yoksa oluştur. Retry doğal yakınsar:
// zarf kaybolsa bile ikinci deneme aynı SKU'yu bulur, kopya ürün oluşamaz.
// Rule 5 (2026-08-09): Agent slice Commands/Queries'e gitmez — Create/Update mantığı KENDİ İÇİNDE
// (Commands.CreateProduct/UpdateProduct kopyası, bilinçli tekrar). İnvariant'lar Product aggregate'te.
// 016: marka/kategori artık ad değil Id alır — Id'ler zincirin Brand/CategoryWrite adımlarından gelir (R10).
// İkisi de zorunludur (kullanıcı kararı 2026-07-27): kategorisiz ürün yazılamaz.
public static class UpsertProductForAgent
{
    public record UpsertProductCommand(
        string Name,
        string Description,
        decimal Price,
        string Sku,
        Guid BrandId,
        Guid CategoryId,
        string? ImageUrl);

    public class UpsertProductResponse
    {
        public Guid Id { get; set; }
        public string Action { get; set; } = default!; // "created" | "updated"
    }

    [Transactional]
    public class UpsertProductCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpsertProductResponse>> Handle(
            UpsertProductCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            // 016: marka ve kategori zorunludur ve var olmalıdır (doğum yalnız feed'den).
            var brand = await session.LoadAsync<Brand>(cmd.BrandId, ct);
            if (brand is null || brand.IsDeleted)
                return FeatureObjectResultModel<UpsertProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.BrandId),
                    Code = CatalogResourceConstants.RECORD_NOT_FOUND
                });

            var category = await session.LoadAsync<Category>(cmd.CategoryId, ct);
            if (category is null || category.IsDeleted)
                return FeatureObjectResultModel<UpsertProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.CategoryId),
                    Code = CatalogResourceConstants.RECORD_NOT_FOUND
                });

            var existing = await session.Query<Product>()
                .FirstOrDefaultAsync(x => x.Sku == cmd.Sku && !x.IsDeleted, ct);

            // 040: ad/SKU zorunluluğu handler'da, fiyat guard'ı Money VO'da (staging konvansiyonu).
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<UpsertProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.PRODUCT_NAME_REQUIRED
                });
            if (string.IsNullOrWhiteSpace(cmd.Sku))
                return FeatureObjectResultModel<UpsertProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Sku),
                    Code = CatalogResourceConstants.PRODUCT_SKU_REQUIRED
                });

            var price = Money.Create(cmd.Price);
            if (price is null)
                return FeatureObjectResultModel<UpsertProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Price),
                    Code = CatalogResourceConstants.PRODUCT_PRICE_NEGATIVE
                });

            Product product;
            string action;
            if (existing is null)
            {
                // Feed → model eşlemesi (data-model): Description iki alana aynı değerle yazılır.
                product = Product.Create(cmd.Name, cmd.Sku, ProductType.Simple, price,
                    cmd.Description, cmd.Description);
                action = "created";
            }
            else
            {
                var rename = existing.Rename(cmd.Name);
                if (!rename.IsSuccess)
                    return FeatureObjectResultModel<UpsertProductResponse>.Error(rename.Messages);
                existing.UpdateDescriptions(cmd.Description, cmd.Description);
                existing.SetPrice(price);
                product = existing;
                action = "updated";
            }

            product.SetBrand(cmd.BrandId);
            product.SetImage(cmd.ImageUrl);

            // K4: dış kontrat tek kategori görür — hedef atanmamışsa eskiler sökülür, yeni primary olur.
            if (product.Categories.All(c => c.CategoryId != cmd.CategoryId))
            {
                foreach (var link in product.Categories.ToList())
                    product.RemoveFromCategory(link.CategoryId);
                var assign = product.AssignToCategory(cmd.CategoryId, isFeatured: false, displayOrder: 0);
                if (!assign.IsSuccess)
                    return FeatureObjectResultModel<UpsertProductResponse>.Error(assign.Messages);
            }

            product.Publish(); // K8: yazılan ürün vitrindedir (Published bayrağıyla parity)
            session.Store(product);

            // 003-storefront-read-model: writer-publishes — Storefront'un CatalogInfo'sunu besler.
            // 016: fat event kimlik + adı birlikte taşır (R7); tüketici Catalog'a lookup yapmaz.
            // 040 K2/K4: kontrat SABİT — decimal fiyat = Price.Amount, kategori = primary atama.
            await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                product.Id, product.Name, product.FullDescription, product.Price.Amount,
                brand.Id, brand.Name, category.Id, category.Name,
                product.ImageUrl, IsDeleted: false));

            return FeatureObjectResultModel<UpsertProductResponse>.Ok(new UpsertProductResponse
            {
                Id = product.Id,
                Action = action
            });
        }
    }
}
