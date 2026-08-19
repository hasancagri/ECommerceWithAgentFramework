namespace CustomNopCommerce.Domains.Customers.Features.Queries;

/// <summary>Bir müşterinin adres defterini (varsayılan işaretleriyle) listeleyen read-slice'ı.</summary>
public static class ListCustomerAddresses
{
    public record ListCustomerAddressesQuery(Guid CustomerId);

    public class AddressItem
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = default!;
        public string City { get; set; } = default!;
        public string Address1 { get; set; } = default!;
        public bool IsDefaultBilling { get; set; }
        public bool IsDefaultShipping { get; set; }
    }

    public class ListCustomerAddressesQueryHandler
    {
        public async Task<FeatureListResultModel<AddressItem>> Handle(
            ListCustomerAddressesQuery query, IQuerySession session, CancellationToken ct)
        {
            var customer = await session.LoadAsync<Customer>(query.CustomerId, ct);
            if (customer is null || customer.IsDeleted)
                return FeatureListResultModel<AddressItem>.NotFound();

            var items = customer.Addresses.Select(a => new AddressItem
            {
                Id = a.Id,
                FullName = $"{a.FirstName} {a.LastName}",
                City = a.City,
                Address1 = a.Address1,
                IsDefaultBilling = customer.DefaultBillingAddressId == a.Id,
                IsDefaultShipping = customer.DefaultShippingAddressId == a.Id,
            }).ToList();

            return FeatureListResultModel<AddressItem>.Ok(items);
        }
    }
}

public static class ListCustomerAddressesQueryEndpoint
{
    public static RouteGroupBuilder ListCustomerAddressesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/addresses", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListCustomerAddresses.AddressItem>>(
                    new ListCustomerAddresses.ListCustomerAddressesQuery(id));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListCustomerAddresses");
        return group;
    }
}
