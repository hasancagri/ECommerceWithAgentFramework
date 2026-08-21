namespace Catalog.Api.Domains.SpecificationAttributes.Features.Commands;

public static class CreateSpecificationAttribute
{
    public record CreateSpecificationAttributeCommand(string Name, bool Filterable, int DisplayOrder);

    public class CreateSpecificationAttributeResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateSpecificationAttributeCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateSpecificationAttributeResponse>> Handle(
            CreateSpecificationAttributeCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var created = SpecificationAttribute.Create(cmd.Name, cmd.Filterable, cmd.DisplayOrder);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<CreateSpecificationAttributeResponse>.Error(created.Messages);

            var attribute = created.Data!;
            var exists = await session.Query<SpecificationAttribute>()
                .AnyAsync(x => x.NormalizedName == attribute.NormalizedName, ct);
            if (exists)
                return FeatureObjectResultModel<CreateSpecificationAttributeResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = CatalogResourceConstants.SPEC_ALREADY_EXISTS });

            session.Store(attribute);
            return FeatureObjectResultModel<CreateSpecificationAttributeResponse>.Ok(
                new CreateSpecificationAttributeResponse { Id = attribute.Id });
        }
    }
}

public static class CreateSpecificationAttributeCommandEndpoint
{
    public static RouteGroupBuilder CreateSpecificationAttributeGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/",
                async ([FromBody] CreateSpecificationAttribute.CreateSpecificationAttributeCommand cmd, IMessageBus bus) =>
                {
                    var result = await bus
                        .InvokeAsync<FeatureObjectResultModel<CreateSpecificationAttribute.CreateSpecificationAttributeResponse>>(cmd);
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CreateSpecificationAttribute");
        return group;
    }
}
