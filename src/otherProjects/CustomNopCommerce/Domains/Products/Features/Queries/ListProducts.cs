namespace CustomNopCommerce.Domains.Products.Features.Queries;

/// <summary>Ürünleri listeleyen read-slice'ı. Basit liste; sayfalama/filtre ileride eklenir.</summary>
public static class ListProducts
{
    public record ListProductsQuery;

    public class ListProductsItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Sku { get; set; } = default!;
        public decimal Price { get; set; }
        public bool Published { get; set; }
    }

    public class ListProductsQueryHandler
    {
        public async Task<FeatureListResultModel<ListProductsItem>> Handle(
            ListProductsQuery query, IQuerySession session, CancellationToken ct)
        {
            var products = await session.Query<Product>()
                .Where(p => !p.IsDeleted)
                .ToListAsync(ct);

            var items = products.Select(p => new ListProductsItem
            {
                Id = p.Id,
                Name = p.Name,
                Sku = p.Sku,
                Price = p.Price.Amount,
                Published = p.Published,
            }).ToList();

            return FeatureListResultModel<ListProductsItem>.Ok(items);
        }
    }
}

public static class ListProductsQueryEndpoint
{
    public static RouteGroupBuilder ListProductsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListProducts.ListProductsItem>>(
                    new ListProducts.ListProductsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListProducts");
        return group;
    }
}
