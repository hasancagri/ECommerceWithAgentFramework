namespace Library.Api.Domains.PriceAlarms;

public static class PriceAlarmEndpointExtension
{
    public static void AddPriceAlarmGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/library/price-alarms")
            .WithTags("PriceAlarms")
            .WithApiVersionSet(apiVersionSet)
            .CreatePriceAlarmGroupItemEndpoint()
            .RemovePriceAlarmGroupItemEndpoint()
            .GetPriceAlarmStatusGroupItemEndpoint();
    }
}