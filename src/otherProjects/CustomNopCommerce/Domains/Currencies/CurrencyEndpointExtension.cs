using CustomNopCommerce.Domains.Currencies.Features.Commands;
using CustomNopCommerce.Domains.Currencies.Features.Queries;

namespace CustomNopCommerce.Domains.Currencies;

/// <summary>Para birimi feature endpoint'lerini tek grup altında toplar.</summary>
public static class CurrencyEndpointExtension
{
    public static void AddCurrencyGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/currencies").WithTags("Currencies")
            .CreateCurrencyGroupItemEndpoint()
            .UpdateCurrencyRateGroupItemEndpoint()
            .ListCurrenciesGroupItemEndpoint();
    }
}
