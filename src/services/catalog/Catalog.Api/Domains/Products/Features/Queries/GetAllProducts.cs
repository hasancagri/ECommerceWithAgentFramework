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
        public Guid BrandId { get; set; }
        public string Brand { get; set; } = null!;
        public Guid? CategoryId { get; set; }
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }

        public static ProductResponse From(Product p, string brand, string? category) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            Sku = p.Sku,
            BrandId = p.BrandId,
            Brand = brand,
            CategoryId = p.CategoryId,
            Category = category,
            ImageUrl = p.ImageUrl,
            IsActive = p.IsActive
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

            // 016: adlar tek sorguyla toplanır (marka/kategori sayısı küçük); response kimlik + ad taşır.
            var brandNames = (await session.Query<Brand>().ToListAsync(ct)).ToDictionary(x => x.Id, x => x.Name);
            var categoryNames = (await session.Query<Category>().ToListAsync(ct)).ToDictionary(x => x.Id, x => x.Name);

            var response = products.Select(p => GetAllProducts.ProductResponse.From(
                p,
                brandNames.GetValueOrDefault(p.BrandId, string.Empty),
                p.CategoryId is null ? null : categoryNames.GetValueOrDefault(p.CategoryId.Value))).ToList();
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