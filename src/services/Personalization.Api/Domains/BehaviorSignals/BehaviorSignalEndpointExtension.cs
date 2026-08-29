namespace Personalization.Api.Domains.BehaviorSignals;

public static class BehaviorSignalEndpointExtension
{
    public static void AddSignalGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/signals")
            .WithTags("Signals")
            .WithApiVersionSet(apiVersionSet)
            .IngestBehaviorSignalsGroupItemEndpoint();
    }
}

public static class IngestBehaviorSignalsEndpoint
{
    // 048 US2: batch gezinme sinyali ingest. Yalniz personalization.ingest scope (WebApp makine token).
    public static RouteGroupBuilder IngestBehaviorSignalsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async (
                [FromBody] IReadOnlyList<IngestBehaviorSignals.BehaviorSignalItemDto> signals,
                IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(
                    new IngestBehaviorSignals.IngestBehaviorSignalsCommand(signals));
                return result.IsSuccess ? Results.Accepted() : Results.BadRequest(result);
            })
            .WithName("IngestBehaviorSignals")
            .RequireAuthorization(AuthorizationScopes.PersonalizationIngest);
        return group;
    }
}