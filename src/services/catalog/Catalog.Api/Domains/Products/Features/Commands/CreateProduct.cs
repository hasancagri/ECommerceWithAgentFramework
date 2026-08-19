namespace Catalog.Api.Domains.Products.Features.Commands;

public static class CreateProduct
{
    public record CreateProductCommand(
        string Name,
        string Description,
        decimal Price,
        string Sku,
        Guid BrandId,
        Guid CategoryId,
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
            // 016: marka ve kategori zorunludur ve var olmalıdır (doğum yalnız feed'den).
            var brand = await session.LoadAsync<Brand>(cmd.BrandId, ct);
            if (brand is null || brand.IsDeleted)
                return FeatureObjectResultModel<CreateProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.BrandId),
                    Code = CatalogResourceConstants.RECORD_NOT_FOUND
                });

            var category = await session.LoadAsync<Category>(cmd.CategoryId, ct);
            if (category is null || category.IsDeleted)
                return FeatureObjectResultModel<CreateProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.CategoryId),
                    Code = CatalogResourceConstants.RECORD_NOT_FOUND
                });

            // 040: ad/SKU zorunluluğu handler'da, fiyat guard'ı Money VO'da (staging konvansiyonu).
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.PRODUCT_NAME_REQUIRED
                });
            if (string.IsNullOrWhiteSpace(cmd.Sku))
                return FeatureObjectResultModel<CreateProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Sku),
                    Code = CatalogResourceConstants.PRODUCT_SKU_REQUIRED
                });

            var price = Money.Create(cmd.Price);
            if (price is null)
                return FeatureObjectResultModel<CreateProductResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Price),
                    Code = CatalogResourceConstants.PRODUCT_PRICE_NEGATIVE
                });

            // Feed → model eşlemesi (data-model): Description iki alana aynı değerle yazılır.
            var product = Product.Create(cmd.Name, cmd.Sku, ProductType.Simple, price,
                cmd.Description, cmd.Description);
            product.SetBrand(cmd.BrandId);
            product.SetImage(cmd.ImageUrl);

            var assign = product.AssignToCategory(cmd.CategoryId, isFeatured: false, displayOrder: 0);
            if (!assign.IsSuccess)
                return FeatureObjectResultModel<CreateProductResponse>.Error(assign.Messages);

            // K8: yazılan ürün vitrindedir — bugünkü davranış Published bayrağıyla eşitlenir.
            product.Publish();
            session.Store(product);

            // 014 (feed = stoğun tek otoritesi): stok tohumlama kaldırıldı; stok yalnız ingestion
            // StockWrite'tan yazılır. Catalog artık stok adedi taşımaz (ProductCreatedEvent öldü).
            // 003-storefront-read-model: writer-publishes — Storefront'un CatalogInfo'sunu besler.
            // 016: fat event kimlik + adı birlikte taşır (R7); tüketici Catalog'a lookup yapmaz.
            // 040 K2/K4: kontrat SABİT — decimal fiyat = Price.Amount, kategori = primary atama.
            await bus.PublishAsync(new IntegrationEvents.ProductChangedEvent(
                product.Id, product.Name, product.FullDescription, product.Price.Amount,
                brand.Id, brand.Name, category.Id, category.Name,
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