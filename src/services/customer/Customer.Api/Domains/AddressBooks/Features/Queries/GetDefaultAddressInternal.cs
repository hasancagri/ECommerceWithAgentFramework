using Customer.Api.Domains.AddressBooks.Entities;

namespace Customer.Api.Domains.AddressBooks.Features.Queries;

// 077: Order.Api hosted-CF ödemesinde siparişi kullanıcının varsayılan adresine bağlar → adresi YAPISAL
// (S2S) kanaldan çeker (makine token customer.read). Varsayılan yoksa ilk kayıtlı adres; hiç yoksa NotFound
// (Order dostça Result hatası döner). Adres MCP/agent'a bu uçtan sızmaz — yalnız internal.
public static class GetDefaultAddressInternal
{
    public record GetDefaultAddressQuery(Guid UserId);

    public class DefaultAddressView
    {
        public string Province { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string Line { get; set; } = string.Empty;
    }

    public class GetDefaultAddressQueryHandler
    {
        public async Task<FeatureObjectResultModel<DefaultAddressView>> Handle(
            GetDefaultAddressQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(b => b.UserId == query.UserId, ct);

            var target = book?.Addresses.FirstOrDefault(a => a.IsDefault) ?? book?.Addresses.FirstOrDefault();
            if (target is null)
                return FeatureObjectResultModel<DefaultAddressView>.NotFound();

            var v = target.Value;
            return FeatureObjectResultModel<DefaultAddressView>.Ok(new DefaultAddressView
            {
                Province = v.Province,
                District = v.District,
                Street = v.Street,
                ZipCode = v.ZipCode,
                Line = v.Line
            });
        }
    }
}
