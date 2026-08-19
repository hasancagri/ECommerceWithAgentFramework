namespace CustomNopCommerce.Domains.Customers.Features.Commands;

/// <summary>Müşteri profil alanlarını güncelleyen write-slice'ı.</summary>
public static class UpdateCustomerProfile
{
    public record UpdateCustomerProfileCommand(
        Guid Id,
        string? FirstName,
        string? LastName,
        string? Gender,
        DateTime? DateOfBirth,
        string? Company,
        string? Phone,
        string? VatNumber);

    [Transactional]
    public class UpdateCustomerProfileCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            UpdateCustomerProfileCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var customer = await session.LoadAsync<Customer>(cmd.Id, ct);
            if (customer is null || customer.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = customer.UpdateProfile(cmd.FirstName, cmd.LastName, cmd.Gender, cmd.DateOfBirth,
                cmd.Company, cmd.Phone, cmd.VatNumber);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(customer);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class UpdateCustomerProfileCommandEndpoint
{
    public static RouteGroupBuilder UpdateCustomerProfileGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id,
            [FromBody] UpdateCustomerProfile.UpdateCustomerProfileCommand body, IMessageBus bus) =>
            {
                var cmd = body with { Id = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("UpdateCustomerProfile");
        return group;
    }
}
