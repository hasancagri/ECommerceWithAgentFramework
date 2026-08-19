namespace CustomNopCommerce.Domains.Warehouses.Features.Commands;

/// <summary>Yeni depo oluşturma write-slice'ı.</summary>
public static class CreateWarehouse
{
    public record CreateWarehouseCommand(string Name, string? AdminComment, Guid? AddressId);

    public class CreateWarehouseResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateWarehouseCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateWarehouseResponse>> Handle(
            CreateWarehouseCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateWarehouseResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = ShippingResourceConstants.WAREHOUSE_NAME_REQUIRED });

            var warehouse = Warehouse.Create(cmd.Name, cmd.AdminComment, cmd.AddressId);
            session.Store(warehouse);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateWarehouseResponse>.Ok(
                new CreateWarehouseResponse { Id = warehouse.Id });
        }
    }
}

public static class CreateWarehouseCommandEndpoint
{
    public static RouteGroupBuilder CreateWarehouseGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateWarehouse.CreateWarehouseCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateWarehouse.CreateWarehouseResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateWarehouse");
        return group;
    }
}
