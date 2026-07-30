namespace Customer.Api.Domains.AddressBooks.Features.Agent;

// MCP (okuma-yalniz) icin adres listeleme slice'i. list_addresses tool'u bunu IMessageBus ile sarar.
public static class GetAddresses
{
    public record GetAddressesQuery(Guid UserId);

    public class AddressView
    {
        public Guid Id { get; set; }
        public string Province { get; set; } = default!;
        public string District { get; set; } = default!;
        public string Street { get; set; } = default!;
        public string ZipCode { get; set; } = default!;
        public string Line { get; set; } = default!;
        public bool IsDefault { get; set; }

        public static AddressView From(SavedAddress a) => new()
        {
            Id = a.Id,
            Province = a.Value.Province,
            District = a.Value.District,
            Street = a.Value.Street,
            ZipCode = a.Value.ZipCode,
            Line = a.Value.Line,
            IsDefault = a.IsDefault
        };
    }

    public class GetAddressesQueryHandler
    {
        public async Task<FeatureListResultModel<AddressView>> Handle(
            GetAddressesQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == query.UserId, ct);

            var views = book?.Addresses.Select(AddressView.From).ToList() ?? new List<AddressView>();
            return FeatureListResultModel<AddressView>.Ok(views);
        }
    }
}