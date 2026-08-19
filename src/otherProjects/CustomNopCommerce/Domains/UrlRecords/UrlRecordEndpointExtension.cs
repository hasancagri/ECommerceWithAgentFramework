using CustomNopCommerce.Domains.UrlRecords.Features.Commands;
using CustomNopCommerce.Domains.UrlRecords.Features.Queries;

namespace CustomNopCommerce.Domains.UrlRecords;

/// <summary>SEO slug (UrlRecord) feature endpoint'lerini tek grup altında toplar.</summary>
public static class UrlRecordEndpointExtension
{
    public static void AddUrlRecordGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/url-records").WithTags("UrlRecords")
            .SetSlugGroupItemEndpoint()
            .GetBySlugGroupItemEndpoint()
            .ListSlugHistoryGroupItemEndpoint();
    }
}
