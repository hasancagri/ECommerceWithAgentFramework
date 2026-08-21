namespace Catalog.Api.Domains.SpecificationAttributes.Features.Commands;

public static class AddSpecificationAttributeOption
{
    public record AddSpecificationAttributeOptionCommand(Guid AttributeId, string Name, int DisplayOrder);

    public class AddSpecificationAttributeOptionResponse
    {
        public Guid OptionId { get; set; }
    }

    [Transactional]
    public class AddSpecificationAttributeOptionCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddSpecificationAttributeOptionResponse>> Handle(
            AddSpecificationAttributeOptionCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var attribute = await session.LoadAsync<SpecificationAttribute>(cmd.AttributeId, ct);
            if (attribute is null)
                return FeatureObjectResultModel<AddSpecificationAttributeOptionResponse>.Error(new MessageItem
                { Property = nameof(cmd.AttributeId), Code = CatalogResourceConstants.SPEC_NOT_FOUND });

            var added = attribute.AddOption(cmd.Name, cmd.DisplayOrder);
            if (!added.IsSuccess)
                return FeatureObjectResultModel<AddSpecificationAttributeOptionResponse>.Error(added.Messages);

            session.Store(attribute);
            return FeatureObjectResultModel<AddSpecificationAttributeOptionResponse>.Ok(
                new AddSpecificationAttributeOptionResponse { OptionId = added.Data });
        }
    }
}

public static class AddSpecificationAttributeOptionCommandEndpoint
{
    public static RouteGroupBuilder AddSpecificationAttributeOptionGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{attributeId:guid}/options",
                async (Guid attributeId, [FromBody] OptionBody body, IMessageBus bus) =>
                {
                    var cmd = new AddSpecificationAttributeOption.AddSpecificationAttributeOptionCommand(
                        attributeId, body.Name, body.DisplayOrder);
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<AddSpecificationAttributeOption.AddSpecificationAttributeOptionResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("AddSpecificationAttributeOption");
        return group;
    }

    public record OptionBody(string Name, int DisplayOrder);
}
