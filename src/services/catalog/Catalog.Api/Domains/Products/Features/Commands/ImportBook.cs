namespace Catalog.Api.Domains.Products.Features.Commands;

// 051: kitap toplu import slice'ı. BookImportHostedService her kitap için IMessageBus ile çağırır.
// İdempotent upsert: ProductId ISBN'den deterministik türer → re-run aynı satırı ezer, çoğaltma yok.
// Yayın kapısı fiyat>0 (Product.Publish); yalnız yayınlanan kitap Stock + Storefront'a event yayar.
// Endpoint YOK: açılış seeder'ı, kullanıcı-akışı değil (İLKE V N/A; JIT iskelet).
public static class ImportBook
{
    public record ImportBookCommand(
        string Isbn,          // kimlik; ProductId deterministik türetilir; Gtin+Sku+barkod
        string Title,
        string Brand,         // dataset brand alanı verbatim (get-or-create)
        decimal? PriceTry,    // null = fiyatsız → taslak kalır
        string? ImageUrl,
        string CategoryMid,
        string CategoryLeaf);

    public class ImportBookResponse
    {
        public Guid ProductId { get; set; }
        public bool Published { get; set; }
    }

    // Sabit InitialStock (research D10): dataset güvenilir adet taşımaz; her yayınlanan kitap 100 ile başlar.
    private const int InitialStock = 100;

    [Transactional]
    public class ImportBookCommandHandler
    {
        public async Task<FeatureObjectResultModel<ImportBookResponse>> Handle(
            ImportBookCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var brand = await GetOrCreateBrandAsync(session, cmd.Brand, ct);
            var mid = await GetOrCreateCategoryAsync(session, cmd.CategoryMid, parentId: null, ct);
            var leaf = await GetOrCreateCategoryAsync(session, cmd.CategoryLeaf, parentId: mid.Id, ct);

            // Fiyat: null → 0 (taslak kalır). Money.Create negatifte null döner; import negatif taşımaz.
            var price = Money.Create(cmd.PriceTry ?? 0m) ?? Money.Zero();

            // İdempotency = ISBN'le bulun-veya-kur (Gtin index'li; seeder sıralı → yarış yok). Re-run
            // aynı ISBN'i günceller, çoğaltmaz. Id türetmeye gerek yok — servisler ProductId'yi event'ten alır.
            var product = await session.Query<Product>().FirstOrDefaultAsync(p => p.Gtin == cmd.Isbn, ct);
            if (product is null)
            {
                product = Product.Create(cmd.Title, cmd.Isbn, ProductType.Simple, price, "", "");
            }
            else
            {
                product.Rename(cmd.Title);
                product.SetPrice(price);
            }

            product.SetIdentifiers(cmd.Isbn, gtin: cmd.Isbn, manufacturerPartNumber: null);
            product.SetBrand(brand.Id);
            product.SetImage(cmd.ImageUrl);
            // Primary kategori = leaf; re-run'da zaten atanmışsa çift-atama guard'ı sessiz reddeder.
            product.AssignToCategory(leaf.Id, isFeatured: false, displayOrder: 0);

            // Yayın kapısı fiyat>0 (aggregate invariant). Başarısız (fiyatsız) → Draft, event YAYILMAZ.
            var publish = product.Publish();
            session.Store(product);

            if (publish.IsSuccess)
            {
                // Yayınlanan kitap omurgayı besler: Stock ilk OnHand + Storefront vitrin satırı.
                await bus.PublishAsync(new IntegrationEvents.ProductAdded(cmd.Isbn, product.Id, InitialStock));
                await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                    product.Id, product.Name, "", product.Price.Amount,
                    brand.Id, brand.Name, leaf.Id, leaf.Name,
                    product.ImageUrl, IsDeleted: false));
            }

            return FeatureObjectResultModel<ImportBookResponse>.Ok(new ImportBookResponse
            {
                ProductId = product.Id,
                Published = product.Published,
            });
        }



        // Brand get-or-create (NormalizedName teklik; 016 düzeni). Verbatim ad — yorumlanmaz.
        private static async Task<Brand> GetOrCreateBrandAsync(IDocumentSession session, string name, CancellationToken ct)
        {
            var normalized = NameNormalization.Normalize(name);
            var existing = await session.Query<Brand>().FirstOrDefaultAsync(b => b.NormalizedName == normalized, ct);
            if (existing is not null)
                return existing;

            var brand = Brand.Create(name).Data!; // İş1 boş brand'i "Unknown"a çevirdi; Create hep başarılı
            session.Store(brand);
            return brand;
        }

        // Category get-or-create ağacı (mid parent, leaf child). SetPublished(true) — tür ağacı görünür.
        private static async Task<Category> GetOrCreateCategoryAsync(
            IDocumentSession session, string name, Guid? parentId, CancellationToken ct)
        {
            var normalized = NameNormalization.Normalize(name);
            var existing = await session.Query<Category>().FirstOrDefaultAsync(c => c.NormalizedName == normalized, ct);
            if (existing is not null)
                return existing;

            var category = Category.Create(name, parentCategoryId: parentId).Data!;
            category.SetPublished(true);
            session.Store(category);
            return category;
        }
    }
}