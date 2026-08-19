namespace CustomNopCommerce.Domains.Vendors.Features.Commands;

/// <summary>Yeni satıcı kaydı oluşturma write-slice'ı.</summary>
public static class RegisterVendor
{
    public record RegisterVendorCommand(string Name, string Email, string Description, Guid? AddressId, int DisplayOrder);

    public class RegisterVendorResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class RegisterVendorCommandHandler
    {
        public async Task<FeatureObjectResultModel<RegisterVendorResponse>> Handle(
            RegisterVendorCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<RegisterVendorResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = VendorsResourceConstants.VENDOR_NAME_REQUIRED });
            if (string.IsNullOrWhiteSpace(cmd.Email))
                return FeatureObjectResultModel<RegisterVendorResponse>.Error(new MessageItem
                { Property = nameof(cmd.Email), Code = VendorsResourceConstants.VENDOR_EMAIL_REQUIRED });
            if (!cmd.Email.Contains('@') || !cmd.Email.Contains('.'))
                return FeatureObjectResultModel<RegisterVendorResponse>.Error(new MessageItem
                { Property = nameof(cmd.Email), Code = VendorsResourceConstants.VENDOR_EMAIL_INVALID });

            var vendor = Vendor.Create(cmd.Name, cmd.Email, cmd.Description, cmd.AddressId, cmd.DisplayOrder);
            session.Store(vendor);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<RegisterVendorResponse>.Ok(
                new RegisterVendorResponse { Id = vendor.Id });
        }
    }
}

public static class RegisterVendorCommandEndpoint
{
    public static RouteGroupBuilder RegisterVendorGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] RegisterVendor.RegisterVendorCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<RegisterVendor.RegisterVendorResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("RegisterVendor");
        return group;
    }
}
