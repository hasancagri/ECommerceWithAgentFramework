namespace CustomNopCommerce.Domains.Customers.Features.Queries;

/// <summary>Tek müşteriyi (profil + varsayılan adres Id'leriyle) getiren read-slice'ı.</summary>
public static class GetCustomer
{
    public record GetCustomerQuery(Guid Id);

    public class GetCustomerResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = default!;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool Active { get; set; }
        public int AddressCount { get; set; }
        public Guid? DefaultBillingAddressId { get; set; }
        public Guid? DefaultShippingAddressId { get; set; }
    }

    public class GetCustomerQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetCustomerResponse>> Handle(
            GetCustomerQuery query, IQuerySession session, CancellationToken ct)
        {
            var customer = await session.LoadAsync<Customer>(query.Id, ct);
            if (customer is null || customer.IsDeleted)
                return FeatureObjectResultModel<GetCustomerResponse>.NotFound();

            return FeatureObjectResultModel<GetCustomerResponse>.Ok(new GetCustomerResponse
            {
                Id = customer.Id,
                Email = customer.Email,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Active = customer.Active,
                AddressCount = customer.Addresses.Count,
                DefaultBillingAddressId = customer.DefaultBillingAddressId,
                DefaultShippingAddressId = customer.DefaultShippingAddressId,
            });
        }
    }
}

public static class GetCustomerQueryEndpoint
{
    public static RouteGroupBuilder GetCustomerGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetCustomer.GetCustomerResponse>>(new GetCustomer.GetCustomerQuery(id));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetCustomer");
        return group;
    }
}
