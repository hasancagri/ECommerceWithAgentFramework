namespace Catalog.Api.Domains.Products.Features.Commands;

public static class UpdateProduct
{
    public record UpdateProductCommand(
        Guid Id,
        string Name,
        string Description,
        decimal Price,
        string Sku,
        List<Guid> AuthorIds,
        Guid PublisherId,
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

            var publisher = await session.LoadAsync<Publisher>(cmd.PublisherId, ct);
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

            product.UpdateDescriptions(cmd.Description, cmd.Description);
            product.SetPrice(price);
            var setAuthors = product.SetAuthors(authors.Select(a => a.Id));
            if (!setAuthors.IsSuccess)
                return FeatureObjectResultModel<UpdateProductResponse>.Error(setAuthors.Messages);
            var setPublisher = product.SetPublisher(cmd.PublisherId);
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

            product.Publish(); // K8: yazım yolu publish eder (parity: yazılan ürün vitrindedir)
            session.Store(product);

            // 003-storefront-read-model: writer-publishes — Storefront'un CatalogInfo'sunu besler.
            // 016: fat event kimlik + adı birlikte taşır (R7).
            // 040 K2/K4: kontrat SABİT — decimal fiyat = Price.Amount, kategori = primary atama.
            await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                product.Id, product.Name, product.FullDescription, product.Price.Amount,
                authors.Select(a => new IntegrationEvents.AuthorRef(a.Id, a.Name)).ToList(),
                publisher.Id, publisher.Name, category.Id, category.Name,
                product.ImageUrl, IsDeleted: false));

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
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("UpdateProduct");
        return group;
    }
}