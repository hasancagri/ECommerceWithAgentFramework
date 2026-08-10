using Customer.Api.Domains.MerchantInformations.Features.Commands;

namespace Customer.Api.Domains.MerchantInformations;

public static class MerchantInformationEndpointExtension
{
    public static void AddMerchantInformationGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/merchant-information")
            .WithTags("MerchantInformation")
            .WithApiVersionSet(apiVersionSet)
            .SetMerchantInformationGroupItemEndpoint()
            .RequireAuthorization();
    }
}
