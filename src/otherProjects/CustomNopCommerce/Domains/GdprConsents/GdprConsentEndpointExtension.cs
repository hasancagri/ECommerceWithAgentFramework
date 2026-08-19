using CustomNopCommerce.Domains.GdprConsents.Features.Commands;
using CustomNopCommerce.Domains.GdprConsents.Features.Queries;

namespace CustomNopCommerce.Domains.GdprConsents;

/// <summary>GDPR rıza tanımı feature endpoint'lerini tek grup altında toplar.</summary>
public static class GdprConsentEndpointExtension
{
    public static void AddGdprConsentGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/gdpr-consents").WithTags("GdprConsents")
            .CreateGdprConsentGroupItemEndpoint()
            .ListGdprConsentsGroupItemEndpoint();
    }
}
