namespace Catalog.Api.Domains.Products.Features.Commands;

public static class CreateProduct
{
    [InvalidatesCache("catalog-products")]
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record CreateProductCommand(
        string Name,
        string Description,
        decimal Price,
        string Sku,
        BrandType Brand,
        string? ImageUrl,
        int InitialStock);

    public class CreateProductResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateProductCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateProductResponse>> Handle(
            CreateProductCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var product = Product.Create(cmd.Name, cmd.Description, cmd.Price, cmd.Sku, cmd.Brand, cmd.ImageUrl);
            session.Store(product);

            await bus.PublishAsync(new IntegrationEvents.ProductCreatedEvent(
                [new ProductStockInfo(product.Id, cmd.InitialStock)]));

            // 003-storefront-read-model: writer-publishes — Storefront'un CatalogInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                product.Id, product.Name, product.ImageUrl, IsDeleted: false));

            return FeatureObjectResultModel<CreateProductResponse>.Ok(new CreateProductResponse { Id = product.Id });
        }
    }
}

public static class CreateProductCommandEndpoint
{
    public static RouteGroupBuilder CreateProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateProduct.CreateProductCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateProduct.CreateProductResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateProduct")
            .RequireAuthorization(AuthorizationScopes.CatalogWrite);
        return group;
    }
}