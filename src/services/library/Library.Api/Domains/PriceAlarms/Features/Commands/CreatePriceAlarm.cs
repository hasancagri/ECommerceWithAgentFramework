namespace Library.Api.Domains.PriceAlarms.Features.Commands;

public static class CreatePriceAlarm
{
    // Email endpoint'te WebApp'in ilettigi cookie claim snapshot'idir (R3) — worker kimseye sormaz.
    [Common.Utils.Authorization.RequiredScope(AuthorizationScopes.LibraryWrite)]
    public record CreatePriceAlarmCommand(
        Guid UserId,
        string Email,
        Guid ProductId,
        string ProductName,
        decimal CurrentPrice);

    [Transactional]
    public class CreatePriceAlarmCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            CreatePriceAlarmCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // FR-002: ayni kullanici + urune tek alarm — mevcut kayit varsa idempotent Ok.
            var exists = await session.Query<PriceAlarm>()
                .Where(x => x.UserId == cmd.UserId && x.ProductId == cmd.ProductId)
                .AnyAsync(ct);
            if (exists)
                return FeatureResultModel.Ok();

            var created = PriceAlarm.Create(
                cmd.UserId, cmd.Email, cmd.ProductId, cmd.ProductName, cmd.CurrentPrice);
            if (!created.IsSuccess)
                return FeatureResultModel.Error(created.Messages);

            session.Store(created.Data!);
            return FeatureResultModel.Ok();
        }
    }
}

public static class CreatePriceAlarmCommandEndpoint
{
    public record CreatePriceAlarmRequest(Guid ProductId, string ProductName, decimal CurrentPrice, string Email);

    public static RouteGroupBuilder CreatePriceAlarmGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreatePriceAlarmRequest request, HttpContext httpContext,
                ICurrentUser currentUser, IMessageBus bus) =>
            {
                var user = currentUser.Load(httpContext.User);

                var result = await bus.InvokeAsync<FeatureResultModel>(
                    new CreatePriceAlarm.CreatePriceAlarmCommand(
                        user.Id, request.Email, request.ProductId, request.ProductName, request.CurrentPrice));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("CreatePriceAlarm")
            .RequireAuthorization(AuthorizationScopes.LibraryWrite);
        return group;
    }
}