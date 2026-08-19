namespace CustomNopCommerce.Domains.ShippingMethods.Features.Queries;

/// <summary>Kargo yöntemlerini sıralı listeleyen read-slice'ı (ücret kuralıyla).</summary>
public static class ListShippingMethods
{
    public record ListShippingMethodsQuery;

    public class ShippingMethodItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int DisplayOrder { get; set; }
        public decimal FlatRate { get; set; }
        public decimal? FreeShippingThreshold { get; set; }
    }

    public class ListShippingMethodsQueryHandler
    {
        public async Task<FeatureListResultModel<ShippingMethodItem>> Handle(
            ListShippingMethodsQuery query, IQuerySession session, CancellationToken ct)
        {
            var methods = await session.Query<ShippingMethod>()
                .Where(m => !m.IsDeleted)
                .ToListAsync(ct);

            var items = methods
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new ShippingMethodItem
                {
                    Id = m.Id,
                    Name = m.Name,
                    DisplayOrder = m.DisplayOrder,
                    FlatRate = m.RateRule.FlatRate,
                    FreeShippingThreshold = m.RateRule.FreeShippingThreshold,
                }).ToList();

            return FeatureListResultModel<ShippingMethodItem>.Ok(items);
        }
    }
}

public static class ListShippingMethodsQueryEndpoint
{
    public static RouteGroupBuilder ListShippingMethodsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListShippingMethods.ShippingMethodItem>>(
                    new ListShippingMethods.ListShippingMethodsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListShippingMethods");
        return group;
    }
}
