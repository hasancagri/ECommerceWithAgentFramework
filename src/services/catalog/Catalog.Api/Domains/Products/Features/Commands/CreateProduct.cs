namespace Catalog.Api.Domains.Products.Features.Commands;

public static class CreateProduct
{
    [InvalidatesCache("catalog-products")]
    public record CreateProductCommand(
        string Name,
        string Description,
        decimal Price,
        string Sku,
        Guid BrandId,
        Guid? CategoryId,
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
            // 016: marka zorunlu ve var olmalı; kategori opsiyonel ama verilmişse var olmalı (doğum yalnız feed'den).
            var brand = await session.LoadAsync<Brand>(cmd.BrandId, ct);
            if (brand is null || brand.IsDeleted)
                return FeatureObjectResultModel<CreateProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.BrandId),
                    Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                });

            Category? category = null;
            if (cmd.CategoryId is not null)
            {
                category = await session.LoadAsync<Category>(cmd.CategoryId.Value, ct);
                if (category is null || category.IsDeleted)
                    return FeatureObjectResultModel<CreateProductResponse>.Error(new MessageItem
                    {
                        Property = nameof(cmd.CategoryId),
                        Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND
                    });
            }

            var product = Product.Create(cmd.Name, cmd.Description, cmd.Price, cmd.Sku,
                cmd.BrandId, cmd.CategoryId, cmd.ImageUrl);
            session.Store(product);

            // 014 (feed = stoğun tek otoritesi): stok tohumlama kaldırıldı; stok yalnız ingestion
            // StockWrite'tan yazılır. Catalog artık stok adedi taşımaz (ProductCreatedEvent öldü).
            // 003-storefront-read-model: writer-publishes — Storefront'un CatalogInfo'sunu besler.
            // 016: fat event kimlik + adı birlikte taşır (R7); tüketici Catalog'a lookup yapmaz.
            await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                product.Id, product.Name, product.Description, product.Price,
                brand.Id, brand.Name, category?.Id, category?.Name,
                product.ImageUrl, IsDeleted: false));

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