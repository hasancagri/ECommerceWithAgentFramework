namespace Customer.Api.Domains.AddressBooks.Features.Agents;

// 062: MCP yazma slice'ı — agent chat'ten varsayılan adresi belirler. İzole handler (bkz. AddAddressForAgent).
public static class SetDefaultAddressForAgent
{
    [RequiredScope(AuthorizationScopes.CustomerWrite)]
    [InvalidatesCache("addresses")]
    public record SetDefaultAddressCommand(Guid UserId, Guid AddressId);

    public class SetDefaultAddressResponse
    {
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class SetDefaultAddressCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetDefaultAddressResponse>> Handle(
            SetDefaultAddressCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (book is null)
                return FeatureObjectResultModel<SetDefaultAddressResponse>.NotFound();

            var result = book.SetDefaultAddress(cmd.AddressId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<SetDefaultAddressResponse>.Error(result.Messages);

            session.Store(book);
            return FeatureObjectResultModel<SetDefaultAddressResponse>.Ok(
                new SetDefaultAddressResponse { Message = "Varsayılan adres güncellendi." });
        }
    }
}