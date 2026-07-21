namespace Catalog.Api.Domains.Products.Features.Queries;

public static class GetAllProducts
{
    [Cached("catalog-products", 60)]
    public record GetAllProductsQuery();

    public class ProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }
        public string Sku { get; set; } = null!;
        public BrandType Brand { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public bool IsComplete { get; set; }
        public bool IsOnSale { get; set; }

        public static ProductResponse From(Product p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Sku = p.Sku,
            Brand = p.Brand,
            ImageUrl = p.ImageUrl,
            IsActive = p.IsActive,
            IsComplete = p.IsComplete,
            IsOnSale = p.IsOnSale
        };
    }
    
    public class GetAllProductsQueryHandler
    {
        public async Task<FeatureObjectResultModel<List<GetAllProducts.ProductResponse>>> Handle(
            GetAllProductsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var products = await session.Query<Product>()
                .Where(x => !x.IsDeleted)
                .ToListAsync(ct);

            var response = products.Select(GetAllProducts.ProductResponse.From).ToList();
            return FeatureObjectResultModel<List<GetAllProducts.ProductResponse>>.Ok(response);
        }
    }
}

public static class GetAllProductsQueryEndpoint
{
    public static RouteGroupBuilder GetAllProductsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetAllProducts.ProductResponse>>>(
                    new GetAllProducts.GetAllProductsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("GetAllProducts")
            .AllowAnonymous();
        return group;
    }
}