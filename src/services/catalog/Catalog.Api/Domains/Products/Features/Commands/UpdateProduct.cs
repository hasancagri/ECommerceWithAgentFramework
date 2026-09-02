namespace Catalog.Api.Domains.Products.Features.Commands;

// 058: admin düzenleme formunun yazma ucu. Yazar/yayınevi Id İLE seçilir; listede olmayan ad
// NewAuthorNames/NewPublisherName ile gelir ve get-or-create edilir (ImportBook deseni — bilinçli tekrar).
// Fiyat gerçekten değişirse ProductPriceChange satırı AYNI session'da yazılır (058 fiyat geçmişi).
// Yayın durumu KORUNUR (eski K8 "her kayıt publish eder" davranışı 058'de kalktı — yayın anahtarı
// SetProductPublished'ta); ProductChangedEvent yalnız yayındaki ürün için yayılır (draft vitrinde yok).
public static class UpdateProduct
{
    public record UpdateProductCommand(
        Guid Id,
        string Name,
        string ShortDescription,
        string FullDescription,
        decimal Price,
        string Sku,
        List<Guid> AuthorIds,
        List<string>? NewAuthorNames,
        Guid? PublisherId,
        string? NewPublisherName,
        Guid CategoryId,
        string? ImageUrl);

    public class UpdateProductResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class UpdateProductCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateProductResponse>> Handle(
            UpdateProductCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<UpdateProductResponse>.NotFound();

            var authors = new List<Author>();
            foreach (var authorId in (cmd.AuthorIds ?? []).Distinct())
            {
                var author = await session.LoadAsync<Author>(authorId, ct);
                if (author is null || author.IsDeleted)
                    return FeatureObjectResultModel<UpdateProductResponse>.Error(new MessageItem
                    { Property = nameof(cmd.AuthorIds), Code = CatalogResourceConstants.RECORD_NOT_FOUND });
                authors.Add(author);
            }

            // 058: listede olmayan yazar adları get-or-create (NormalizedName teklik — ImportBook deseni).
            foreach (var name in (cmd.NewAuthorNames ?? []).Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                var normalized = NameNormalization.Normalize(name);
                var existing = authors.FirstOrDefault(a => a.NormalizedName == normalized)
                               ?? await session.Query<Author>()
                                   .FirstOrDefaultAsync(a => a.NormalizedName == normalized, ct);
                if (existing is null)
                {
                    existing = Author.Create(name).Data!;
                    session.Store(existing);
                }

                if (authors.All(a => a.Id != existing.Id))
                    authors.Add(existing);
            }

            Publisher? publisher = null;
            if (!string.IsNullOrWhiteSpace(cmd.NewPublisherName))
            {
                var normalized = NameNormalization.Normalize(cmd.NewPublisherName);
                publisher = await session.Query<Publisher>()
                    .FirstOrDefaultAsync(p => p.NormalizedName == normalized, ct);
                if (publisher is null)
                {
                    publisher = Publisher.Create(cmd.NewPublisherName).Data!;
                    session.Store(publisher);
                }
            }
            else if (cmd.PublisherId is { } publisherId)
            {
                publisher = await session.LoadAsync<Publisher>(publisherId, ct);
            }

            if (publisher is null || publisher.IsDeleted)
                return FeatureObjectResultModel<UpdateProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.PublisherId),
                    Code = CatalogResourceConstants.RECORD_NOT_FOUND
                });

            var category = await session.LoadAsync<Category>(cmd.CategoryId, ct);
            if (category is null || category.IsDeleted)
                return FeatureObjectResultModel<UpdateProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.CategoryId),
                    Code = CatalogResourceConstants.RECORD_NOT_FOUND
                });

            // 040: eski tek-Update yerine davranış metotları zinciri; ilk hata Result'ı döner.
            var price = Money.Create(cmd.Price);
            if (price is null)
                return FeatureObjectResultModel<UpdateProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Price),
                    Code = CatalogResourceConstants.PRODUCT_PRICE_NEGATIVE
                });

            var rename = product.Rename(cmd.Name);
            if (!rename.IsSuccess)
                return FeatureObjectResultModel<UpdateProductResponse>.Error(rename.Messages);

            var identifiers = product.SetIdentifiers(cmd.Sku, product.Gtin, product.ManufacturerPartNumber);
            if (!identifiers.IsSuccess)
                return FeatureObjectResultModel<UpdateProductResponse>.Error(identifiers.Messages);

            product.UpdateDescriptions(cmd.ShortDescription, cmd.FullDescription);

            // 058 FR-013: yalnız GERÇEK fiyat değişimi geçmişe satır düşürür (aynı fiyatla kayıt düşmez).
            var oldPrice = product.Price.Amount;
            product.SetPrice(price);
            if (oldPrice != price.Amount)
                session.Store(ProductPriceChange.Create(product.Id, oldPrice, price.Amount, DateTime.UtcNow));

            var setAuthors = product.SetAuthors(authors.Select(a => a.Id));
            if (!setAuthors.IsSuccess)
                return FeatureObjectResultModel<UpdateProductResponse>.Error(setAuthors.Messages);
            var setPublisher = product.SetPublisher(publisher.Id);
            if (!setPublisher.IsSuccess)
                return FeatureObjectResultModel<UpdateProductResponse>.Error(setPublisher.Messages);
            product.SetImage(cmd.ImageUrl);

            // K4: dış kontrat tek kategori görür — hedef kategori atanmamışsa eski atamalar sökülüp
            // yenisi primary yazılır (bugünkü "kategori değiştir" davranışının çoklu-model karşılığı).
            if (product.Categories.All(c => c.CategoryId != cmd.CategoryId))
            {
                foreach (var link in product.Categories.ToList())
                    product.RemoveFromCategory(link.CategoryId);
                var assign = product.AssignToCategory(cmd.CategoryId, isFeatured: false, displayOrder: 0);
                if (!assign.IsSuccess)
                    return FeatureObjectResultModel<UpdateProductResponse>.Error(assign.Messages);
            }

            session.Store(product);

            // 003-storefront-read-model: writer-publishes — Storefront'un CatalogInfo'sunu besler.
            // 058: yalnız yayındaki ürün event yayar; draft düzenlemesi vitrine sızmaz.
            if (product.Published)
            {
                await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                    product.Id, product.Name, product.FullDescription, product.Price.Amount,
                    authors.Select(a => new IntegrationEvents.AuthorRef(a.Id, a.Name)).ToList(),
                    publisher.Id, publisher.Name, category.Id, category.Name,
                    product.ImageUrl, IsDeleted: false,
                    // 060: yalnız GERÇEK fiyat değişiminde dolu — Library alarm tetiğini bundan verir.
                    OldPrice: oldPrice != price.Amount ? oldPrice : null));
            }

            return FeatureObjectResultModel<UpdateProductResponse>.Ok(new UpdateProductResponse { Id = product.Id });
        }
    }
}

public static class UpdateProductCommandEndpoint
{
    public static RouteGroupBuilder UpdateProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/", async ([FromBody] UpdateProduct.UpdateProductCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<UpdateProduct.UpdateProductResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("UpdateProduct");
        return group;
    }
}