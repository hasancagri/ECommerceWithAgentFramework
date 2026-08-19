using CustomNopCommerce.Domains.Products.ValueObjects;

namespace CustomNopCommerce.Domains.Products.Features.Commands;

/// <summary>Yeni ürün oluşturma write-slice'ı (CQRS command).</summary>
public static class CreateProduct
{
    public record CreateProductCommand(
        string Name,
        string Sku,
        ProductType Type,
        decimal Price,
        string ShortDescription,
        string FullDescription);

    public class CreateProductResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateProductCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateProductResponse>> Handle(
            CreateProductCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = CatalogResourceConstants.PRODUCT_NAME_REQUIRED });
            if (string.IsNullOrWhiteSpace(cmd.Sku))
                return FeatureObjectResultModel<CreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.Sku), Code = CatalogResourceConstants.PRODUCT_SKU_REQUIRED });

            var price = Money.Create(cmd.Price);
            if (price is null)
                return FeatureObjectResultModel<CreateProductResponse>.Error(new MessageItem
                { Property = nameof(cmd.Price), Code = CatalogResourceConstants.PRODUCT_PRICE_NEGATIVE });

            var product = Product.Create(cmd.Name, cmd.Sku, cmd.Type, price, cmd.ShortDescription, cmd.FullDescription);
            session.Store(product);
            await session.SaveChangesAsync(ct);

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
