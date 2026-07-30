namespace Customer.Api.Domains.AddressBooks.Features.Commands;

public static class DeleteAddress
{
    public record DeleteAddressCommand(Guid UserId, Guid AddressId);

    [Transactional]
    public class DeleteAddressCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            DeleteAddressCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (book is null)
                return FeatureResultModel.NotFound();

            var result = book.RemoveAddress(cmd.AddressId);
            if (!result.IsSuccess)
                return result;

            session.Store(book);
            return FeatureResultModel.Ok();
        }
    }
}

public static class DeleteAddressCommandEndpoint
{
    public static RouteGroupBuilder DeleteAddressGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, IMessageBus bus) =>
            {
                var userId = CurrentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureResultModel>(new DeleteAddress.DeleteAddressCommand(userId, id));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("DeleteAddress")
            .RequireAuthorization(AuthorizationScopes.CustomerWrite);
        return group;
    }
}