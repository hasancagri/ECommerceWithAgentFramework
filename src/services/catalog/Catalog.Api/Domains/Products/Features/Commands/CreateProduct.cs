namespace Catalog.Api.Domains.Products.Features.Commands;

public static class CreateProduct
{
    [InvalidatesCache("catalog-products")]
    public record CreateProductCommand(
        string Name,
        string Description,
        decimal Price,
        string Sku,
        BrandType Brand,
        string? ImageUrl);

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

            // 014 (feed = stoğun tek otoritesi): stok tohumlama kaldırıldı; stok yalnız ingestion
            // StockWrite'tan yazılır. Catalog artık stok adedi taşımaz (ProductCreatedEvent öldü).
            // 003-storefront-read-model: writer-publishes — Storefront'un CatalogInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                product.Id, product.Name, product.Description, product.Price,
                product.Brand.ToString(), product.ImageUrl, IsDeleted: false));

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
            .WithName("CreateProduct");
        return group;
    }
}