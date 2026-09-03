namespace Customer.Api.Domains.AddressBooks.Features.Agents;

// 063: MCP yazma slice'ı — agent chat'ten mevcut bir adresi günceller. İzole handler (bkz. AddAddressForAgent).
public static class UpdateAddressForAgent
{
    [RequiredScope(AuthorizationScopes.CustomerWrite)]
    [InvalidatesCache("addresses")]
    public record UpdateAddressCommand(
        Guid UserId, Guid AddressId, string Province, string District, string Street, string ZipCode, string Line);

    public class UpdateAddressResponse
    {
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class UpdateAddressCommandHandler
    {
        public async Task<FeatureObjectResultModel<UpdateAddressResponse>> Handle(
            UpdateAddressCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var address = Address.Create(cmd.Province, cmd.District, cmd.Street, cmd.ZipCode, cmd.Line);
            if (!address.IsSuccess)
                return FeatureObjectResultModel<UpdateAddressResponse>.Error(address.Messages);

            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (book is null)
                return FeatureObjectResultModel<UpdateAddressResponse>.NotFound();

            var result = book.UpdateAddress(cmd.AddressId, address.Data!);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<UpdateAddressResponse>.Error(result.Messages);

            session.Store(book);
            return FeatureObjectResultModel<UpdateAddressResponse>.Ok(
                new UpdateAddressResponse { Message = "Adres güncellendi." });
        }
    }
}