using CustomNopCommerce.Domains.Vendors.Features.Commands;
using CustomNopCommerce.Domains.Vendors.Features.Queries;

namespace CustomNopCommerce.Domains.Vendors;

/// <summary>Satıcı feature endpoint'lerini tek grup altında toplar.</summary>
public static class VendorEndpointExtension
{
    public static void AddVendorGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/vendors").WithTags("Vendors")
            .RegisterVendorGroupItemEndpoint()
            .AddVendorNoteGroupItemEndpoint()
            .ListVendorsGroupItemEndpoint();
    }
}
