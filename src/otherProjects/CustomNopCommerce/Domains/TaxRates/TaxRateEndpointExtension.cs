using CustomNopCommerce.Domains.TaxRates.Features.Commands;
using CustomNopCommerce.Domains.TaxRates.Features.Queries;

namespace CustomNopCommerce.Domains.TaxRates;

/// <summary>Vergi oranı feature endpoint'lerini tek grup altında toplar.</summary>
public static class TaxRateEndpointExtension
{
    public static void AddTaxRateGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/tax-rates").WithTags("TaxRates")
            .CreateTaxRateGroupItemEndpoint()
            .ListTaxRatesByCategoryGroupItemEndpoint();
    }
}
