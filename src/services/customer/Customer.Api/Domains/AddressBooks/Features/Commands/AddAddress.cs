namespace Customer.Api.Domains.AddressBooks.Features.Commands;

public static class AddAddress
{
    public record AddAddressCommand(
        Guid UserId,
        string Province,
        string District,
        string Street,
        string ZipCode,
        string Line);

    public class AddAddressResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class AddAddressCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddAddressResponse>> Handle(
            AddAddressCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var address = Address.Create(cmd.Province, cmd.District, cmd.Street, cmd.ZipCode, cmd.Line);
            if (!address.IsSuccess)
                return FeatureObjectResultModel<AddAddressResponse>.Error(address.Messages);

            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            book ??= AddressBook.Create(cmd.UserId);

            var saved = book.AddAddress(address.Data!);

            session.Store(book);
            return FeatureObjectResultModel<AddAddressResponse>.Ok(new AddAddressResponse { Id = saved.Id });
        }
    }
}

public static class AddAddressCommandEndpoint
{
    public static RouteGroupBuilder AddAddressGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("", async ([FromBody] AddAddress.AddAddressCommand cmd, HttpContext httpContext, IMessageBus bus) =>
            {
                var userId = CurrentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AddAddress.AddAddressResponse>>(cmd with { UserId = userId });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("AddAddress")
            .RequireAuthorization(AuthorizationScopes.CustomerWrite);
        return group;
    }
}