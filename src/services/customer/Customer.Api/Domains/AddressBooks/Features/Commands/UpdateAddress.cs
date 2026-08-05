namespace Customer.Api.Domains.AddressBooks.Features.Commands;

public static class UpdateAddress
{
    [InvalidatesCache("addresses")]
    public record UpdateAddressCommand(
        Guid UserId,
        Guid AddressId,
        string Province,
        string District,
        string Street,
        string ZipCode,
        string Line);

    [Transactional]
    public class UpdateAddressCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            UpdateAddressCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var address = Address.Create(cmd.Province, cmd.District, cmd.Street, cmd.ZipCode, cmd.Line);
            if (!address.IsSuccess)
                return FeatureResultModel.Error(address.Messages);

            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (book is null)
                return FeatureResultModel.NotFound();

            var result = book.UpdateAddress(cmd.AddressId, address.Data!);
            if (!result.IsSuccess)
                return result;

            session.Store(book);
            return FeatureResultModel.Ok();
        }
    }
}

public static class UpdateAddressCommandEndpoint
{
    public record UpdateAddressBody(string Province, string District, string Street, string ZipCode, string Line);

    public static RouteGroupBuilder UpdateAddressGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateAddressBody body, HttpContext httpContext, ICurrentUser currentUser, IMessageBus bus) =>
            {
                var userId = currentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureResultModel>(new UpdateAddress.UpdateAddressCommand(
                    userId, id, body.Province, body.District, body.Street, body.ZipCode, body.Line));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("UpdateAddress")
            .RequireAuthorization(AuthorizationScopes.CustomerWrite);
        return group;
    }
}