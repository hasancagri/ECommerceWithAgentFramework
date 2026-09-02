namespace Library.Api.Domains.PriceAlarms.Features.Queries;

public static class GetPriceAlarmStatus
{
    [Common.Utils.Authorization.RequiredScope(AuthorizationScopes.LibraryRead)]
    public record GetPriceAlarmStatusQuery(Guid UserId, Guid ProductId);

    public class GetPriceAlarmStatusResponse
    {
        public bool Exists { get; set; }
    }

    public class GetPriceAlarmStatusQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetPriceAlarmStatusResponse>> Handle(
            GetPriceAlarmStatusQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var exists = await session.Query<PriceAlarm>()
                .Where(x => x.UserId == query.UserId && x.ProductId == query.ProductId)
                .AnyAsync(ct);

            return FeatureObjectResultModel<GetPriceAlarmStatusResponse>.Ok(
                new GetPriceAlarmStatusResponse { Exists = exists });
        }
    }
}

public static class GetPriceAlarmStatusQueryEndpoint
{
    public static RouteGroupBuilder GetPriceAlarmStatusGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{productId:guid}", async (Guid productId, HttpContext httpContext,
                ICurrentUser currentUser, IMessageBus bus) =>
            {
                var user = currentUser.Load(httpContext.User);

                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetPriceAlarmStatus.GetPriceAlarmStatusResponse>>(
                    new GetPriceAlarmStatus.GetPriceAlarmStatusQuery(user.Id, productId));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("GetPriceAlarmStatus")
            .RequireAuthorization(AuthorizationScopes.LibraryRead);
        return group;
    }
}