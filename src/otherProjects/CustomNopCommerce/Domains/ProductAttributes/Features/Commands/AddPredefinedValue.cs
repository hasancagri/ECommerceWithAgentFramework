namespace CustomNopCommerce.Domains.ProductAttributes.Features.Commands;

/// <summary>Global özniteliğe önceden tanımlı değer şablonu (ör. Beden→M) ekleme write-slice'ı.</summary>
public static class AddPredefinedValue
{
    public record AddPredefinedValueCommand(
        Guid ProductAttributeId,
        string Name,
        decimal PriceAdjustment,
        bool UsePercentage,
        bool IsPreSelected,
        int DisplayOrder);

    [Transactional]
    public class AddPredefinedValueCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            AddPredefinedValueCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var attribute = await session.LoadAsync<ProductAttribute>(cmd.ProductAttributeId, ct);
            if (attribute is null || attribute.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = attribute.AddPredefinedValue(cmd.Name, cmd.PriceAdjustment, cmd.UsePercentage,
                cmd.IsPreSelected, cmd.DisplayOrder);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(attribute);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class AddPredefinedValueCommandEndpoint
{
    public static RouteGroupBuilder AddPredefinedValueGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/predefined-values", async (Guid id,
            [FromBody] AddPredefinedValue.AddPredefinedValueCommand body, IMessageBus bus) =>
            {
                var cmd = body with { ProductAttributeId = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("AddPredefinedValue");
        return group;
    }
}
