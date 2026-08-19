using CustomNopCommerce.Domains.ShippingMethods.ValueObjects;

namespace CustomNopCommerce.Domains.ShippingMethods.Features.Commands;

/// <summary>Yeni kargo yöntemi oluşturma write-slice'ı.</summary>
public static class CreateShippingMethod
{
    public record CreateShippingMethodCommand(
        string Name,
        string Description,
        int DisplayOrder,
        decimal FlatRate,
        decimal? FreeShippingThreshold);

    public class CreateShippingMethodResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateShippingMethodCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateShippingMethodResponse>> Handle(
            CreateShippingMethodCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateShippingMethodResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = ShippingResourceConstants.METHOD_NAME_REQUIRED });

            var rateRule = ShippingRateRule.Create(cmd.FlatRate, cmd.FreeShippingThreshold);
            if (rateRule is null)
                return FeatureObjectResultModel<CreateShippingMethodResponse>.Error(new MessageItem
                { Property = nameof(cmd.FlatRate), Code = ShippingResourceConstants.METHOD_RATE_INVALID });

            var method = ShippingMethod.Create(cmd.Name, cmd.Description, cmd.DisplayOrder, rateRule);
            session.Store(method);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateShippingMethodResponse>.Ok(
                new CreateShippingMethodResponse { Id = method.Id });
        }
    }
}

public static class CreateShippingMethodCommandEndpoint
{
    public static RouteGroupBuilder CreateShippingMethodGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateShippingMethod.CreateShippingMethodCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateShippingMethod.CreateShippingMethodResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateShippingMethod");
        return group;
    }
}
