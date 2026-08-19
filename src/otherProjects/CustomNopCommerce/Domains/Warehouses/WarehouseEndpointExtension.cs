using CustomNopCommerce.Domains.Warehouses.Features.Commands;
using CustomNopCommerce.Domains.Warehouses.Features.Queries;

namespace CustomNopCommerce.Domains.Warehouses;

/// <summary>Depo feature endpoint'lerini tek grup altında toplar.</summary>
public static class WarehouseEndpointExtension
{
    public static void AddWarehouseGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/warehouses").WithTags("Warehouses")
            .CreateWarehouseGroupItemEndpoint()
            .ListWarehousesGroupItemEndpoint();
    }
}
