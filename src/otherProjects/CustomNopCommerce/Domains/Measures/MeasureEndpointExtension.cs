using CustomNopCommerce.Domains.Measures.Features.Commands;
using CustomNopCommerce.Domains.Measures.Features.Queries;

namespace CustomNopCommerce.Domains.Measures;

/// <summary>Ölçü birimi feature endpoint'lerini tek grup altında toplar.</summary>
public static class MeasureEndpointExtension
{
    public static void AddMeasureGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/measures").WithTags("Measures")
            .CreateMeasureGroupItemEndpoint()
            .ListMeasuresGroupItemEndpoint();
    }
}
