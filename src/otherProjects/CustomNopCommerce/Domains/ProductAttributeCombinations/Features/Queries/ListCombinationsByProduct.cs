namespace CustomNopCommerce.Domains.ProductAttributeCombinations.Features.Queries;

/// <summary>Bir ürünün satılabilir varyantlarını listeleyen read-slice'ı.</summary>
public static class ListCombinationsByProduct
{
    public record ListCombinationsByProductQuery(Guid ProductId);

    public class CombinationItem
    {
        public Guid Id { get; set; }
        public string Sku { get; set; } = default!;
        public decimal? OverriddenPrice { get; set; }
        public List<Guid> SelectedValueIds { get; set; } = new();
    }

    public class ListCombinationsByProductQueryHandler
    {
        public async Task<FeatureListResultModel<CombinationItem>> Handle(
            ListCombinationsByProductQuery query, IQuerySession session, CancellationToken ct)
        {
            var combinations = await session.Query<ProductAttributeCombination>()
                .Where(c => c.ProductId == query.ProductId && !c.IsDeleted)
                .ToListAsync(ct);

            var items = combinations.Select(c => new CombinationItem
            {
                Id = c.Id,
                Sku = c.Sku,
                OverriddenPrice = c.OverriddenPrice?.Amount,
                SelectedValueIds = c.SelectedValueIds.ToList(),
            }).ToList();

            return FeatureListResultModel<CombinationItem>.Ok(items);
        }
    }
}

public static class ListCombinationsByProductQueryEndpoint
{
    public static RouteGroupBuilder ListCombinationsByProductGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/by-product/{productId:guid}", async (Guid productId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListCombinationsByProduct.CombinationItem>>(
                    new ListCombinationsByProduct.ListCombinationsByProductQuery(productId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListCombinationsByProduct");
        return group;
    }
}
