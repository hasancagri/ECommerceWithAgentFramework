namespace Library.Api.Domains.PriceAlarms.Features.Commands;

public static class RemovePriceAlarm
{
    [Common.Utils.Authorization.RequiredScope(AuthorizationScopes.LibraryWrite)]
    public record RemovePriceAlarmCommand(Guid UserId, Guid ProductId);

    [Transactional]
    public class RemovePriceAlarmCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            RemovePriceAlarmCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var alarm = await session.Query<PriceAlarm>()
                .Where(x => x.UserId == cmd.UserId && x.ProductId == cmd.ProductId)
                .FirstOrDefaultAsync(ct);
            if (alarm is null)
                return FeatureResultModel.Error(new MessageItem
                {
                    Code = LibraryResourceConstants.PRICE_ALARM_NOT_FOUND
                });

            // Kaldirma = hard delete (data-model); yasam dongusu biter, iz NotificationRecord'da kalir.
            session.Delete(alarm);
            return FeatureResultModel.Ok();
        }
    }
}

public static class RemovePriceAlarmCommandEndpoint
{
    public static RouteGroupBuilder RemovePriceAlarmGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{productId:guid}", async (Guid productId, HttpContext httpContext,
                ICurrentUser currentUser, IMessageBus bus) =>
            {
                var user = currentUser.Load(httpContext.User);

                var result = await bus.InvokeAsync<FeatureResultModel>(
                    new RemovePriceAlarm.RemovePriceAlarmCommand(user.Id, productId));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("RemovePriceAlarm")
            .RequireAuthorization(AuthorizationScopes.LibraryWrite);
        return group;
    }
}