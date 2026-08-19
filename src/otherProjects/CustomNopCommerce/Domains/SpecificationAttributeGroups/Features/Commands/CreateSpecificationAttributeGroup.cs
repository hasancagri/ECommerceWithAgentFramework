namespace CustomNopCommerce.Domains.SpecificationAttributeGroups.Features.Commands;

/// <summary>Yeni spesifikasyon grubu oluşturma write-slice'ı.</summary>
public static class CreateSpecificationAttributeGroup
{
    public record CreateSpecificationAttributeGroupCommand(string Name, int DisplayOrder);

    public class CreateSpecificationAttributeGroupResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateSpecificationAttributeGroupCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateSpecificationAttributeGroupResponse>> Handle(
            CreateSpecificationAttributeGroupCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateSpecificationAttributeGroupResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = CatalogResourceConstants.SPEC_GROUP_NAME_REQUIRED });

            var group = SpecificationAttributeGroup.Create(cmd.Name, cmd.DisplayOrder);
            session.Store(group);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateSpecificationAttributeGroupResponse>.Ok(
                new CreateSpecificationAttributeGroupResponse { Id = group.Id });
        }
    }
}

public static class CreateSpecificationAttributeGroupCommandEndpoint
{
    public static RouteGroupBuilder CreateSpecificationAttributeGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateSpecificationAttributeGroup.CreateSpecificationAttributeGroupCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateSpecificationAttributeGroup.CreateSpecificationAttributeGroupResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateSpecificationAttributeGroup");
        return group;
    }
}
