using CustomNopCommerce.Domains.SpecificationAttributeGroups;

namespace CustomNopCommerce.Domains.SpecificationAttributes.Features.Commands;

/// <summary>Yeni spesifikasyon özniteliği (Ekran Boyutu, RAM...) oluşturma write-slice'ı.</summary>
public static class CreateSpecificationAttribute
{
    public record CreateSpecificationAttributeCommand(string Name, int DisplayOrder, Guid? GroupId);

    public class CreateSpecificationAttributeResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateSpecificationAttributeCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateSpecificationAttributeResponse>> Handle(
            CreateSpecificationAttributeCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateSpecificationAttributeResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = CatalogResourceConstants.SPEC_NAME_REQUIRED });

            // Grup verildiyse var olmalı.
            if (cmd.GroupId is { } groupId)
            {
                var group = await session.LoadAsync<SpecificationAttributeGroup>(groupId, ct);
                if (group is null || group.IsDeleted)
                    return FeatureObjectResultModel<CreateSpecificationAttributeResponse>.Error(new MessageItem
                    { Property = nameof(cmd.GroupId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });
            }

            var spec = SpecificationAttribute.Create(cmd.Name, cmd.DisplayOrder, cmd.GroupId);
            session.Store(spec);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateSpecificationAttributeResponse>.Ok(
                new CreateSpecificationAttributeResponse { Id = spec.Id });
        }
    }
}

public static class CreateSpecificationAttributeCommandEndpoint
{
    public static RouteGroupBuilder CreateSpecificationAttributeGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateSpecificationAttribute.CreateSpecificationAttributeCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateSpecificationAttribute.CreateSpecificationAttributeResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateSpecificationAttribute");
        return group;
    }
}
