namespace CustomNopCommerce.Domains.Warehouses.Features.Queries;

/// <summary>Depoları listeleyen read-slice'ı.</summary>
public static class ListWarehouses
{
    public record ListWarehousesQuery;

    public class WarehouseItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public Guid? AddressId { get; set; }
    }

    public class ListWarehousesQueryHandler
    {
        public async Task<FeatureListResultModel<WarehouseItem>> Handle(
            ListWarehousesQuery query, IQuerySession session, CancellationToken ct)
        {
            var warehouses = await session.Query<Warehouse>()
                .Where(w => !w.IsDeleted)
                .ToListAsync(ct);

            var items = warehouses.Select(w => new WarehouseItem
            {
                Id = w.Id,
                Name = w.Name,
                AddressId = w.AddressId,
            }).ToList();

            return FeatureListResultModel<WarehouseItem>.Ok(items);
        }
    }
}

public static class ListWarehousesQueryEndpoint
{
    public static RouteGroupBuilder ListWarehousesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListWarehouses.WarehouseItem>>(
                    new ListWarehouses.ListWarehousesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListWarehouses");
        return group;
    }
}
