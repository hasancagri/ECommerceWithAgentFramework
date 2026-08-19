using CustomNopCommerce.Domains.ProductAttributeMappings.ValueObjects;

namespace CustomNopCommerce.Domains.ProductAttributeMappings.Features.Commands;

/// <summary>Bir eşlemeye seçilebilir değer (ör. Renk eşlemesine "Kırmızı") ekleme write-slice'ı.</summary>
public static class AddAttributeValue
{
    public record AddAttributeValueCommand(
        Guid MappingId,
        string Name,
        AttributeValueType ValueType,
        decimal PriceAdjustment,
        bool UsePercentage,
        decimal WeightAdjustment,
        decimal Cost,
        string? ColorSquaresRgb,
        bool IsPreSelected,
        int DisplayOrder,
        Guid? AssociatedProductId);

    public class AddAttributeValueResponse
    {
        public Guid ValueId { get; set; }
    }

    [Transactional]
    public class AddAttributeValueCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddAttributeValueResponse>> Handle(
            AddAttributeValueCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var mapping = await session.LoadAsync<ProductAttributeMapping>(cmd.MappingId, ct);
            if (mapping is null || mapping.IsDeleted)
                return FeatureObjectResultModel<AddAttributeValueResponse>.NotFound();

            var priceAdjustment = PriceAdjustment.Create(cmd.PriceAdjustment, cmd.UsePercentage);
            var result = mapping.AddValue(cmd.Name, cmd.ValueType, priceAdjustment, cmd.WeightAdjustment,
                cmd.Cost, cmd.ColorSquaresRgb, cmd.IsPreSelected, cmd.DisplayOrder, cmd.AssociatedProductId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<AddAttributeValueResponse>.Error(result.Messages);

            session.Update(mapping);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<AddAttributeValueResponse>.Ok(
                new AddAttributeValueResponse { ValueId = result.Data });
        }
    }
}

public static class AddAttributeValueCommandEndpoint
{
    public static RouteGroupBuilder AddAttributeValueGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/values", async (Guid id,
            [FromBody] AddAttributeValue.AddAttributeValueCommand body, IMessageBus bus) =>
            {
                var cmd = body with { MappingId = id };
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AddAttributeValue.AddAttributeValueResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("AddAttributeValue");
        return group;
    }
}
