namespace CustomNopCommerce.Domains.Products.Features.Queries;

/// <summary>Tek ürünü Id ile getiren read-slice'ı (CQRS query — yalnız okur).</summary>
public static class GetProduct
{
    public record GetProductQuery(Guid Id);

    public class GetProductResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Sku { get; set; } = default!;
        public decimal Price { get; set; }
        public string Currency { get; set; } = default!;
        public bool Published { get; set; }
    }

    public class GetProductQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetProductResponse>> Handle(
            GetProductQuery query, IQuerySession session, CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(query.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<GetProductResponse>.NotFound();

            return FeatureObjectResultModel<GetProductResponse>.Ok(new GetProductResponse
            {
                Id = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                Price = product.Price.Amount,
                Currency = product.Price.Currency,
                Published = product.Published,
            });
        }
    }
}

public static class GetProductQueryEndpoint
{
    public static RouteGroupBuilder GetProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetProduct.GetProductResponse>>(
                    new GetProduct.GetProductQuery(id));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetProduct");
        return group;
    }
}
