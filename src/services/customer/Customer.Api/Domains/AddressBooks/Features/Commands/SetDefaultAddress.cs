namespace Customer.Api.Domains.AddressBooks.Features.Commands;

public static class SetDefaultAddress
{
    public record SetDefaultAddressCommand(Guid UserId, Guid AddressId);

    [Transactional]
    public class SetDefaultAddressCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            SetDefaultAddressCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (book is null)
                return FeatureResultModel.NotFound();

            var result = book.SetDefaultAddress(cmd.AddressId);
            if (!result.IsSuccess)
                return result;

            session.Store(book);
            return FeatureResultModel.Ok();
        }
    }
}

public static class SetDefaultAddressCommandEndpoint
{
    public static RouteGroupBuilder SetDefaultAddressGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/default", async (Guid id, HttpContext httpContext, IMessageBus bus) =>
            {
                var userId = CurrentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureResultModel>(new SetDefaultAddress.SetDefaultAddressCommand(userId, id));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("SetDefaultAddress")
            .RequireAuthorization(AuthorizationScopes.CustomerWrite);
        return group;
    }
}