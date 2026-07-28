namespace Catalog.Api.Domains.Products.Features.Queries;

public static class GetProductByName
{
    public record GetProductByNameQuery(string Name);

    public class ProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }
        public string Sku { get; set; } = null!;
        public Guid BrandId { get; set; }
        public string Brand { get; set; } = null!;
        public Guid CategoryId { get; set; }
        public string Category { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }

        public static ProductResponse From(Product p, string brand, string category) => new()
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

    public class GetProductByNameQueryHandler
    {
        public async Task<FeatureObjectResultModel<ProductResponse>> Handle(
            GetProductByNameQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Isme gore (kismi, buyuk/kucuk harf duyarsiz) en iyi eslesen TEK urun.
            // Musteri aramasi: yalnizca satista (aktif VE tam) urunler gorunur.
            var product = await session.Query<Product>()
                .Where(x => !x.IsDeleted && x.IsActive &&
                            x.Name.Contains(query.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name)
                .FirstOrDefaultAsync(ct);

            if (product is null)
                return FeatureObjectResultModel<GetProductByName.ProductResponse>.Ok(null);

            var brand = await session.LoadAsync<Brand>(product.BrandId, ct);
            var category = await session.LoadAsync<Category>(product.CategoryId, ct);

            return FeatureObjectResultModel<GetProductByName.ProductResponse>.Ok(
                ProductResponse.From(product, brand?.Name ?? string.Empty, category?.Name ?? string.Empty));
        }
    }
}

public static class GetProductByNameQueryEndpoint
{
    public static RouteGroupBuilder GetProductByNameGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/search", async ([FromQuery] string name, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetProductByName.ProductResponse>>(
                    new GetProductByName.GetProductByNameQuery(name));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetProductByName")
            .AllowAnonymous();
        return group;
    }
}