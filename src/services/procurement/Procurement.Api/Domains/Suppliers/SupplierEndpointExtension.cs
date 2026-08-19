using Procurement.Api.Domains.Suppliers.Features.Queries;

namespace Procurement.Api.Domains.Suppliers;

// Supplier okuma penceresi (aggregate-REST kuralı). Mutator'lar (Create/SetCategoryMappings) seed
// sahipliğindedir — REST'e AÇILMAZ (kural istisnası: akış sahibi kanal tek giriştir).
public static class SupplierEndpointExtension
{
    public static void AddSupplierGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("v{version:apiVersion}/suppliers")
            .WithTags("Suppliers")
            .WithApiVersionSet(apiVersionSet);

        group.MapGet("", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetSuppliers.GetSuppliersResponse>>(
                    new GetSuppliers.GetSuppliersQuery());
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("GetSuppliers");
    }
}