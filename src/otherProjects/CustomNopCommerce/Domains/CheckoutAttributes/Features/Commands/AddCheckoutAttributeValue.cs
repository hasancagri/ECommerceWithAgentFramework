namespace CustomNopCommerce.Domains.CheckoutAttributes.Features.Commands;

/// <summary>Bir checkout özniteliğine seçilebilir değer ekleme write-slice'ı.</summary>
public static class AddCheckoutAttributeValue
{
    public record AddCheckoutAttributeValueCommand(
        Guid CheckoutAttributeId,
        string Name,
        decimal PriceAdjustment,
        decimal WeightAdjustment,
        string? ColorSquaresRgb,
        bool IsPreSelected,
        int DisplayOrder);

    public class AddCheckoutAttributeValueResponse
    {
        public Guid ValueId { get; set; }
    }

    [Transactional]
    public class AddCheckoutAttributeValueCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddCheckoutAttributeValueResponse>> Handle(
            AddCheckoutAttributeValueCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var attribute = await session.LoadAsync<CheckoutAttribute>(cmd.CheckoutAttributeId, ct);
            if (attribute is null || attribute.IsDeleted)
                return FeatureObjectResultModel<AddCheckoutAttributeValueResponse>.NotFound();

            var result = attribute.AddValue(cmd.Name, cmd.PriceAdjustment, cmd.WeightAdjustment,
                cmd.ColorSquaresRgb, cmd.IsPreSelected, cmd.DisplayOrder);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AddCheckoutAttributeValueResponse>.Error(result.Messages);

            session.Update(attribute);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<AddCheckoutAttributeValueResponse>.Ok(
                new AddCheckoutAttributeValueResponse { ValueId = result.Data });
        }
    }
}

public static class AddCheckoutAttributeValueCommandEndpoint
{
    public static RouteGroupBuilder AddCheckoutAttributeValueGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/values", async (Guid id,
            [FromBody] AddCheckoutAttributeValue.AddCheckoutAttributeValueCommand body, IMessageBus bus) =>
            {
                var cmd = body with { CheckoutAttributeId = id };
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AddCheckoutAttributeValue.AddCheckoutAttributeValueResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("AddCheckoutAttributeValue");
        return group;
    }
}
