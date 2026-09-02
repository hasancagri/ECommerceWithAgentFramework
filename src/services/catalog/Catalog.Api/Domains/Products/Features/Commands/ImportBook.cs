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
        string[] Authors,     // 052: çok-yazar (her ad get-or-create); en az bir (İş1 "Unknown" fallback)
        string Publisher,     // 052: tek yayınevi (İş1 uydurma, ISBN-kararlı; get-or-create)
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
            var authors = await GetOrCreateAuthorsAsync(session, cmd.Authors, ct);
            var publisher = await GetOrCreatePublisherAsync(session, cmd.Publisher, ct);
            var mid = await GetOrCreateCategoryAsync(session, cmd.CategoryMid, parentId: null, ct);
            // leaf == mid (kategori sadeleştirmesi sonrası olağan): aynı transaction'da ikinci get-or-create
            // commit olmamış mid'i göremez → çift Store → Category NormalizedName unique index ihlali.
            // Aynıysa mid'i doğrudan kullan (ikinci kayıt açma).
            var leaf = NameNormalization.Normalize(cmd.CategoryLeaf) == mid.NormalizedName
                ? mid
                : await GetOrCreateCategoryAsync(session, cmd.CategoryLeaf, parentId: mid.Id, ct);

            // Fiyat: null → 0 (taslak kalır). Money.Create negatifte null döner; import negatif taşımaz.
            var price = Money.Create(cmd.PriceTry ?? 0m) ?? Money.Zero();

            // İdempotency = ISBN'le bulun-veya-kur (Gtin index'li; seeder sıralı → yarış yok). Re-run
            // aynı ISBN'i günceller, çoğaltmaz. Id türetmeye gerek yok — servisler ProductId'yi event'ten alır.
            var product = await session.Query<Product>().FirstOrDefaultAsync(p => p.Gtin == cmd.Isbn, ct);
            if (product is null)
            {
                product = Product.Create(cmd.Title, cmd.Isbn, ProductType.Simple, price, "", "");
                // 058 FR-013: import fiyatı geçmişin İLK satırıdır (OldPrice=null); fiyatsız taslak satır düşürmez.
                if (price.Amount > 0)
                    session.Store(ProductPriceChange.Create(product.Id, oldPrice: null, price.Amount, DateTime.UtcNow));
            }
            else
            {
                product.Rename(cmd.Title);
                // 058 FR-013: re-run'da gerçek fiyat değişimi geçmişe yazılır (0 = "fiyat yoktu" → OldPrice=null).
                var oldPrice = product.Price.Amount;
                product.SetPrice(price);
                if (oldPrice != price.Amount && price.Amount > 0)
                    session.Store(ProductPriceChange.Create(
                        product.Id, oldPrice == 0 ? null : oldPrice, price.Amount, DateTime.UtcNow));
            }

            product.SetIdentifiers(cmd.Isbn, gtin: cmd.Isbn, manufacturerPartNumber: null);
            product.SetAuthors(authors.Select(a => a.Id));
            product.SetPublisher(publisher.Id);
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
                    authors.Select(a => new IntegrationEvents.AuthorRef(a.Id, a.Name)).ToList(),
                    publisher.Id, publisher.Name, leaf.Id, leaf.Name,
                    product.ImageUrl, IsDeleted: false));
            }

            return FeatureObjectResultModel<ImportBookResponse>.Ok(new ImportBookResponse
            {
                ProductId = product.Id,
                Published = product.Published,
            });
        }



        // 052: Author get-or-create listesi (her ad NormalizedName teklik; sıra korunur). Verbatim ad.
        // İş1 boş yazarı ["Unknown"]'a çevirdi → Create hep başarılı. Aynı normalize ad tek Author'a düşer.
        private static async Task<List<Author>> GetOrCreateAuthorsAsync(
            IDocumentSession session, string[] names, CancellationToken ct)
        {
            var result = new List<Author>();
            var seen = new HashSet<string>();
            foreach (var name in names)
            {
                var normalized = NameNormalization.Normalize(name);
                if (!seen.Add(normalized))
                    continue; // aynı kitapta yinelenen yazar adı tekilleşir
                var existing = result.FirstOrDefault(a => a.NormalizedName == normalized)
                               ?? await session.Query<Author>().FirstOrDefaultAsync(a => a.NormalizedName == normalized, ct);
                if (existing is null)
                {
                    existing = Author.Create(name).Data!;
                    session.Store(existing);
                }
                result.Add(existing);
            }
            return result;
        }

        // 052: Publisher get-or-create (NormalizedName teklik; 4 uydurma ad get-or-create'le tekilleşir).
        private static async Task<Publisher> GetOrCreatePublisherAsync(
            IDocumentSession session, string name, CancellationToken ct)
        {
            var normalized = NameNormalization.Normalize(name);
            var existing = await session.Query<Publisher>().FirstOrDefaultAsync(p => p.NormalizedName == normalized, ct);
            if (existing is not null)
                return existing;

            var publisher = Publisher.Create(name).Data!;
            session.Store(publisher);
            return publisher;
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