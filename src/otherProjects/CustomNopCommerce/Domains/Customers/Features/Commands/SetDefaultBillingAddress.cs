namespace CustomNopCommerce.Domains.Customers.Features.Commands;

/// <summary>Müşterinin varsayılan fatura adresini ayarlayan write-slice'ı. Adres defterde olmalı (invariant).</summary>
public static class SetDefaultBillingAddress
{
    public record SetDefaultBillingAddressCommand(Guid CustomerId, Guid AddressId);

    [Transactional]
    public class SetDefaultBillingAddressCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            SetDefaultBillingAddressCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var customer = await session.LoadAsync<Customer>(cmd.CustomerId, ct);
            if (customer is null || customer.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = customer.SetDefaultBillingAddress(cmd.AddressId);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(customer);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class SetDefaultBillingAddressCommandEndpoint
{
    public static RouteGroupBuilder SetDefaultBillingAddressGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/default-billing-address", async (Guid id,
            [FromBody] SetDefaultBillingAddress.SetDefaultBillingAddressCommand body, IMessageBus bus) =>
            {
                var cmd = body with { CustomerId = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("SetDefaultBillingAddress");
        return group;
    }
}
