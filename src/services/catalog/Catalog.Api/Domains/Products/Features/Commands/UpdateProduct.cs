namespace Catalog.Api.Domains.Products.Features.Commands;

public static class UpdateProduct
{
    public record UpdateProductCommand(
        Guid Id,
        string Name,
        string Description,
        decimal Price,
        string Sku,
        Guid BrandId,
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

            var brand = await session.LoadAsync<Brand>(cmd.BrandId, ct);
            if (brand is null || brand.IsDeleted)
                return FeatureObjectResultModel<UpdateProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.BrandId),
                    Code = CatalogResourceConstants.RECORD_NOT_FOUND
                });

            var category = await session.LoadAsync<Category>(cmd.CategoryId, ct);
            if (category is null || category.IsDeleted)
                return FeatureObjectResultModel<UpdateProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.CategoryId),
                    Code = CatalogResourceConstants.RECORD_NOT_FOUND
                });

            var update = product.Update(cmd.Name, cmd.Description, cmd.Price, cmd.Sku,
                cmd.BrandId, cmd.CategoryId, cmd.ImageUrl);
            if (!update.IsSuccess)
                return FeatureObjectResultModel<UpdateProductResponse>.Error(update.Messages);
            session.Store(product);

            // 003-storefront-read-model: writer-publishes — Storefront'un CatalogInfo'sunu besler.
            // 016: fat event kimlik + adı birlikte taşır (R7).
            await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                product.Id, product.Name, product.Description, product.Price,
                brand.Id, brand.Name, category.Id, category.Name,
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