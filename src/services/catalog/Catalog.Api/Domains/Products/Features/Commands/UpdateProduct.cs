namespace Catalog.Api.Domains.Products.Features.Commands;

public static class UpdateProduct
{
    [InvalidatesCache("catalog-products")]
    public record UpdateProductCommand(
        Guid Id,
        string Name,
        string Description,
        decimal Price,
        string Sku,
        BrandType Brand,
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

            product.Update(cmd.Name, cmd.Description, cmd.Price, cmd.Sku, cmd.Brand, cmd.ImageUrl);
            session.Store(product);

            // 003-storefront-read-model: writer-publishes — Storefront'un CatalogInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                product.Id, product.Name, product.ImageUrl, IsDeleted: false));

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