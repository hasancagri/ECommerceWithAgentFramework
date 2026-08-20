namespace Catalog.Api.Domains.Products.Features.Queries;

public static class GetProductById
{
    public record GetProductByIdQuery(Guid Id);

    public class GetProductByIdResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string ShortDescription { get; set; } = default!;
        public string FullDescription { get; set; } = default!;
        public string Sku { get; set; } = default!;
        public string? Gtin { get; set; }
        public string? ManufacturerPartNumber { get; set; }
        public decimal Price { get; set; }
        public bool Published { get; set; }
        public Guid BrandId { get; set; }
        public string? ImageUrl { get; set; }
        public List<Guid> CategoryIds { get; set; } = [];
        public List<Guid> TagIds { get; set; } = [];
        public decimal Weight { get; set; }
        public decimal Length { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
    }

    // Cache YOK (bilinçli): tekil ürün okuması yazma yolunu besleyebilir — CLAUDE.md cache kuralı.
    public class GetProductByIdQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetProductByIdResponse>> Handle(
            GetProductByIdQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(query.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<GetProductByIdResponse>.NotFound();

            return FeatureObjectResultModel<GetProductByIdResponse>.Ok(new GetProductByIdResponse
            {
                Id = product.Id,
                Name = product.Name,
                ShortDescription = product.ShortDescription,
                FullDescription = product.FullDescription,
                Sku = product.Sku,
                Gtin = product.Gtin,
                ManufacturerPartNumber = product.ManufacturerPartNumber,
                Price = product.Price.Amount,
                Published = product.Published,
                BrandId = product.BrandId,
                ImageUrl = product.ImageUrl,
                CategoryIds = product.Categories.Select(x => x.CategoryId).ToList(),
                TagIds = product.TagIds.ToList(),
                Weight = product.Dimensions.Weight,
                Length = product.Dimensions.Length,
                Width = product.Dimensions.Width,
                Height = product.Dimensions.Height
            });
        }
    }
}

public static class GetProductByIdQueryEndpoint
{
    public static RouteGroupBuilder GetProductByIdGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetProductById.GetProductByIdResponse>>(
                    new GetProductById.GetProductByIdQuery(id));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetProductById");
        return group;
    }
}