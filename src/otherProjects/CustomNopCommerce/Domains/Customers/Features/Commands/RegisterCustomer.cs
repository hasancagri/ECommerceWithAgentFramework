namespace CustomNopCommerce.Domains.Customers.Features.Commands;

/// <summary>Yeni müşteri profili oluşturma write-slice'ı. NOT: kimlik doğrulama (parola/login) Identity.Server'da;
/// bu yalnız iş-verisi profilidir.</summary>
public static class RegisterCustomer
{
    public record RegisterCustomerCommand(string Email, string? FirstName, string? LastName);

    public class RegisterCustomerResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class RegisterCustomerCommandHandler
    {
        public async Task<FeatureObjectResultModel<RegisterCustomerResponse>> Handle(
            RegisterCustomerCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Email))
                return FeatureObjectResultModel<RegisterCustomerResponse>.Error(new MessageItem
                { Property = nameof(cmd.Email), Code = CustomersResourceConstants.CUSTOMER_EMAIL_REQUIRED });
            if (!cmd.Email.Contains('@') || !cmd.Email.Contains('.'))
                return FeatureObjectResultModel<RegisterCustomerResponse>.Error(new MessageItem
                { Property = nameof(cmd.Email), Code = CustomersResourceConstants.CUSTOMER_EMAIL_INVALID });

            var customer = Customer.Create(cmd.Email, cmd.FirstName, cmd.LastName);
            session.Store(customer);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<RegisterCustomerResponse>.Ok(
                new RegisterCustomerResponse { Id = customer.Id });
        }
    }
}

public static class RegisterCustomerCommandEndpoint
{
    public static RouteGroupBuilder RegisterCustomerGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] RegisterCustomer.RegisterCustomerCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<RegisterCustomer.RegisterCustomerResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("RegisterCustomer");
        return group;
    }
}
