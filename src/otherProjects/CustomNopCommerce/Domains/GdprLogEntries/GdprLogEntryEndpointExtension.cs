using CustomNopCommerce.Domains.GdprLogEntries.Features.Commands;
using CustomNopCommerce.Domains.GdprLogEntries.Features.Queries;

namespace CustomNopCommerce.Domains.GdprLogEntries;

/// <summary>GDPR denetim kaydı feature endpoint'lerini tek grup altında toplar.</summary>
public static class GdprLogEntryEndpointExtension
{
    public static void AddGdprLogEntryGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/gdpr-log").WithTags("GdprLog")
            .RecordGdprActionGroupItemEndpoint()
            .ListGdprLogByCustomerGroupItemEndpoint();
    }
}
