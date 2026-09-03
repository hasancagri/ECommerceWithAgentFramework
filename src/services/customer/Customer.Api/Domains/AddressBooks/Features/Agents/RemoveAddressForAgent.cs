namespace Customer.Api.Domains.AddressBooks.Features.Agents;

// 062: MCP yazma slice'ı — agent chat'ten adres siler. İzole handler (bkz. AddAddressForAgent).
public static class RemoveAddressForAgent
{
    [RequiredScope(AuthorizationScopes.CustomerWrite)]
    [InvalidatesCache("addresses")]
    public record RemoveAddressCommand(Guid UserId, Guid AddressId);

    public class RemoveAddressResponse
    {
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class RemoveAddressCommandHandler
    {
        public async Task<FeatureObjectResultModel<RemoveAddressResponse>> Handle(
            RemoveAddressCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (book is null)
                return FeatureObjectResultModel<RemoveAddressResponse>.NotFound();

            var result = book.RemoveAddress(cmd.AddressId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RemoveAddressResponse>.Error(result.Messages);

            session.Store(book);
            return FeatureObjectResultModel<RemoveAddressResponse>.Ok(
                new RemoveAddressResponse { Message = "Adres silindi." });
        }
    }
}