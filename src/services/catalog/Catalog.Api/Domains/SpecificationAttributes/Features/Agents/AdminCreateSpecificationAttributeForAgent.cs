namespace Catalog.Api.Domains.SpecificationAttributes.Features.Agents;

// 074: REST admin yüzeyi söküldü — CreateSpecificationAttribute İKİZİ (bilinçli tekrar) agent yüzeyi.
// Kanonik özellik tanımı oluşturur (043); teklik NormalizedName. İz: AdminActionLog (FR-009).
public static class AdminCreateSpecificationAttributeForAgent
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminCreateSpecificationAttributeCommand(
        Guid UserId,
        string Name,
        bool Filterable,
        int DisplayOrder);

    public class AdminCreateSpecificationAttributeResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class AdminCreateSpecificationAttributeForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminCreateSpecificationAttributeResponse>> Handle(
            AdminCreateSpecificationAttributeCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var created = SpecificationAttribute.Create(cmd.Name, cmd.Filterable, cmd.DisplayOrder);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<AdminCreateSpecificationAttributeResponse>.Error(created.Messages);

            var attribute = created.Data!;
            var exists = await session.Query<SpecificationAttribute>()
                .AnyAsync(x => x.NormalizedName == attribute.NormalizedName, ct);
            if (exists)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, Shared.CatalogAdminTools.CreateSpecificationAttribute, "-",
                    "specification attribute already exists"));
                return FeatureObjectResultModel<AdminCreateSpecificationAttributeResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = CatalogResourceConstants.SPEC_ALREADY_EXISTS });
            }

            session.Store(attribute);
            session.Store(AdminAudit.AdminActionLog.Executed(
                cmd.UserId, Shared.CatalogAdminTools.CreateSpecificationAttribute, attribute.Id.ToString(),
                $"created '{attribute.Name}' (filterable={cmd.Filterable})"));

            return FeatureObjectResultModel<AdminCreateSpecificationAttributeResponse>.Ok(
                new AdminCreateSpecificationAttributeResponse { Id = attribute.Id });
        }
    }
}
