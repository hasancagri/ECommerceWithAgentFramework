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

            Product product;
            string action;
            if (existing is null)
            {
                product = Product.Create(cmd.Name, cmd.Description, cmd.Price, cmd.Sku,
                    cmd.BrandId, cmd.CategoryId, cmd.ImageUrl);
                action = "created";
            }
            else
            {
                var update = existing.Update(cmd.Name, cmd.Description, cmd.Price, cmd.Sku,
                    cmd.BrandId, cmd.CategoryId, cmd.ImageUrl);
                if (!update.IsSuccess)
                    return FeatureObjectResultModel<UpsertProductResponse>.Error(update.Messages);
                product = existing;
                action = "updated";
            }
            session.Store(product);

            // 003-storefront-read-model: writer-publishes — Storefront'un CatalogInfo'sunu besler.
            // 016: fat event kimlik + adı birlikte taşır (R7); tüketici Catalog'a lookup yapmaz.
            await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                product.Id, product.Name, product.Description, product.Price,
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
