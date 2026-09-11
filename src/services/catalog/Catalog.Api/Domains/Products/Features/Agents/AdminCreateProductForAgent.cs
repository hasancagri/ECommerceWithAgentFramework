namespace Catalog.Api.Domains.Products.Features.Agents;

// 074: admin künye OLUŞTURMA (agent yüzeyi) — REST CreateProduct söküldü, doktrin kayması: import (051)
// artık tek giriş değil. ISBN mağazada Product.Gtin'de yaşar (ProductId rastgele Guid; ImportBook emsali).
// Çakışma: aynı Gtin varsa Error (çoğaltma yok — agent admin_update_product'a yönlendirir). Yeni ürün
// DRAFT doğar (eski REST auto-publish'ten farklı): yayın ayrı adım (admin_set_published). Draft olduğu için
// ProductChangedEvent YAYILMAZ (AdminUpdateProduct deseni). Fiyat>0 ise geçmişin ilk satırı. İz: AdminActionLog.
public static class AdminCreateProductForAgent
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminCreateProductCommand(
        Guid UserId,
        string Name,
        string Isbn,
        decimal Price,
        string? ShortDescription,
        string? FullDescription,
        List<Guid>? AuthorIds,
        List<string>? NewAuthorNames,
        Guid? PublisherId,
        string? NewPublisherName,
        Guid? CategoryId,
        string? ImageUrl);

    public class AdminCreateProductResponse
    {
        public Guid ProductId { get; set; }
        public string Isbn { get; set; } = default!;
        public bool IsPublished { get; set; }
    }

    [Transactional]
    public class AdminCreateProductForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminCreateProductResponse>> Handle(
            AdminCreateProductCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = CatalogResourceConstants.PRODUCT_NAME_REQUIRED });
            if (string.IsNullOrWhiteSpace(cmd.Isbn))
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.Isbn), Code = CatalogResourceConstants.PRODUCT_SKU_REQUIRED });

            // Kimlik = ISBN (Gtin). Çakışma kontrolü: aynı Gtin varsa çoğaltma yok → Error + iz.
            var existing = await session.Query<Product>().FirstOrDefaultAsync(p => p.Gtin == cmd.Isbn, ct);
            if (existing is not null)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, CatalogAdminTools.CreateProduct, cmd.Isbn, "isbn already exists"));
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.Isbn), Code = CatalogResourceConstants.PRODUCT_ISBN_EXISTS });
            }

            var price = Money.Create(cmd.Price);
            if (price is null)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.Price), Code = CatalogResourceConstants.PRODUCT_PRICE_NEGATIVE });

            // Yazar(lar): AuthorIds mevcut olmalı; NewAuthorNames get-or-create (normalize). En az bir yazar.
            var authors = new List<Author>();
            foreach (var authorId in (cmd.AuthorIds ?? []).Distinct())
            {
                var author = await session.LoadAsync<Author>(authorId, ct);
                if (author is null || author.IsDeleted)
                    return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                    { Property = nameof(cmd.AuthorIds), Code = CatalogResourceConstants.RECORD_NOT_FOUND });
                authors.Add(author);
            }
            foreach (var name in (cmd.NewAuthorNames ?? []).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                var normalized = NameNormalization.Normalize(name);
                var author = authors.FirstOrDefault(a => a.NormalizedName == normalized)
                             ?? await session.Query<Author>().FirstOrDefaultAsync(a => a.NormalizedName == normalized, ct);
                if (author is null)
                {
                    author = Author.Create(name).Data!;
                    session.Store(author);
                }
                if (authors.All(a => a.Id != author.Id))
                    authors.Add(author);
            }
            if (authors.Count == 0)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.AuthorIds), Code = CatalogResourceConstants.PRODUCT_AUTHOR_REQUIRED });

            // Yayınevi: PublisherId YA DA NewPublisherName (get-or-create); zorunlu.
            Publisher? publisher = null;
            if (!string.IsNullOrWhiteSpace(cmd.NewPublisherName))
            {
                var normalized = NameNormalization.Normalize(cmd.NewPublisherName);
                publisher = await session.Query<Publisher>().FirstOrDefaultAsync(p => p.NormalizedName == normalized, ct);
                if (publisher is null)
                {
                    publisher = Publisher.Create(cmd.NewPublisherName).Data!;
                    session.Store(publisher);
                }
            }
            else if (cmd.PublisherId is { } pubId)
            {
                publisher = await session.LoadAsync<Publisher>(pubId, ct);
            }
            if (publisher is null || publisher.IsDeleted)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.PublisherId), Code = CatalogResourceConstants.PRODUCT_PUBLISHER_REQUIRED });

            // Kategori: zorunlu, var olmalı.
            if (cmd.CategoryId is not { } categoryId)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.CategoryId), Code = CatalogResourceConstants.PRODUCT_CATEGORY_REQUIRED });
            var category = await session.LoadAsync<Category>(categoryId, ct);
            if (category is null || category.IsDeleted)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.CategoryId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });

            // Oluştur — SKU=ISBN (ImportBook emsali). Draft doğar (Publish YOK); event yayılmaz.
            var product = Product.Create(cmd.Name, cmd.Isbn, ProductType.Simple, price,
                cmd.ShortDescription ?? string.Empty, cmd.FullDescription ?? string.Empty);
            product.SetIdentifiers(cmd.Isbn, gtin: cmd.Isbn, manufacturerPartNumber: null);

            var setAuthors = product.SetAuthors(authors.Select(a => a.Id));
            if (!setAuthors.IsSuccess)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(setAuthors.Messages);
            var setPublisher = product.SetPublisher(publisher.Id);
            if (!setPublisher.IsSuccess)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(setPublisher.Messages);
            product.SetImage(cmd.ImageUrl);
            var assign = product.AssignToCategory(categoryId, isFeatured: false, displayOrder: 0);
            if (!assign.IsSuccess)
                return FeatureObjectResultModel<AdminCreateProductResponse>.Error(assign.Messages);

            session.Store(product);

            // 058 FR-013: fiyat>0 ise geçmişin ilk satırı (OldPrice=null); fiyatsız taslak satır düşürmez.
            if (price.Amount > 0)
                session.Store(ProductPriceChange.Create(product.Id, oldPrice: null, price.Amount, DateTime.UtcNow));

            session.Store(AdminAudit.AdminActionLog.Executed(
                cmd.UserId, CatalogAdminTools.CreateProduct, product.Id.ToString(),
                $"Created draft '{cmd.Name}' (isbn {cmd.Isbn}, {price.Amount} TL)"));

            return FeatureObjectResultModel<AdminCreateProductResponse>.Ok(new AdminCreateProductResponse
            {
                ProductId = product.Id,
                Isbn = cmd.Isbn,
                IsPublished = product.Published,
            });
        }
    }
}
