namespace CustomNopCommerce.Domains.Customers.Features.Commands;

/// <summary>Müşteri adres defterine adres ekleme write-slice'ı. İlk adres otomatik varsayılan olur.</summary>
public static class AddCustomerAddress
{
    public record AddCustomerAddressCommand(
        Guid CustomerId,
        string FirstName,
        string LastName,
        string? Company,
        Guid? CountryId,
        string City,
        string Address1,
        string? Address2,
        string? ZipPostalCode,
        string? PhoneNumber,
        string? Email);

    public class AddCustomerAddressResponse
    {
        public Guid AddressId { get; set; }
    }

    [Transactional]
    public class AddCustomerAddressCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddCustomerAddressResponse>> Handle(
            AddCustomerAddressCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var customer = await session.LoadAsync<Customer>(cmd.CustomerId, ct);
            if (customer is null || customer.IsDeleted)
                return FeatureObjectResultModel<AddCustomerAddressResponse>.NotFound();

            if (string.IsNullOrWhiteSpace(cmd.Address1))
                return FeatureObjectResultModel<AddCustomerAddressResponse>.Error(new MessageItem
                { Property = nameof(cmd.Address1), Code = CustomersResourceConstants.ADDRESS_LINE_REQUIRED });
            if (string.IsNullOrWhiteSpace(cmd.City))
                return FeatureObjectResultModel<AddCustomerAddressResponse>.Error(new MessageItem
                { Property = nameof(cmd.City), Code = CustomersResourceConstants.ADDRESS_CITY_REQUIRED });

            var address = CustomerAddress.Create(cmd.FirstName, cmd.LastName, cmd.Company, cmd.CountryId,
                cmd.City, cmd.Address1, cmd.Address2, cmd.ZipPostalCode, cmd.PhoneNumber, cmd.Email);
            var result = customer.AddAddress(address);

            session.Update(customer);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<AddCustomerAddressResponse>.Ok(
                new AddCustomerAddressResponse { AddressId = result.Data });
        }
    }
}

public static class AddCustomerAddressCommandEndpoint
{
    public static RouteGroupBuilder AddCustomerAddressGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/addresses", async (Guid id,
            [FromBody] AddCustomerAddress.AddCustomerAddressCommand body, IMessageBus bus) =>
            {
                var cmd = body with { CustomerId = id };
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AddCustomerAddress.AddCustomerAddressResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("AddCustomerAddress");
        return group;
    }
}
