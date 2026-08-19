using CustomNopCommerce.Domains.Countries.Features.Commands;
using CustomNopCommerce.Domains.Countries.Features.Queries;

namespace CustomNopCommerce.Domains.Countries;

/// <summary>Ülke feature endpoint'lerini tek grup altında toplar.</summary>
public static class CountryEndpointExtension
{
    public static void AddCountryGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/countries").WithTags("Countries")
            .CreateCountryGroupItemEndpoint()
            .AddStateProvinceGroupItemEndpoint()
            .ListCountriesGroupItemEndpoint();
    }
}
