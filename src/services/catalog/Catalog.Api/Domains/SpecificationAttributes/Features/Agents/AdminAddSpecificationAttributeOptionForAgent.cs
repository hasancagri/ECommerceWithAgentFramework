namespace Catalog.Api.Domains.SpecificationAttributes.Features.Agents;

// 074: REST admin yüzeyi söküldü — AddSpecificationAttributeOption İKİZİ (bilinçli tekrar) agent yüzeyi.
// Kapalı listeye yeni değer ekler (043); üretilen OptionId döner. İz: AdminActionLog (FR-009);
// bulunamadı da iz bırakır (Rejected + NotFound).
public static class AdminAddSpecificationAttributeOptionForAgent
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminAddSpecificationAttributeOptionCommand(
        Guid UserId,
        Guid AttributeId,
        string Name,
        int DisplayOrder);

    public class AdminAddSpecificationAttributeOptionResponse
    {
        public Guid OptionId { get; set; }
    }

    [Transactional]
    public class AdminAddSpecificationAttributeOptionForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminAddSpecificationAttributeOptionResponse>> Handle(
            AdminAddSpecificationAttributeOptionCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var attribute = await session.LoadAsync<SpecificationAttribute>(cmd.AttributeId, ct);
            if (attribute is null || attribute.IsDeleted)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, Shared.CatalogAdminTools.AddSpecificationAttributeOption,
                    cmd.AttributeId.ToString(), "specification attribute not found"));
                return FeatureObjectResultModel<AdminAddSpecificationAttributeOptionResponse>.NotFound();
            }

            var added = attribute.AddOption(cmd.Name, cmd.DisplayOrder);
            if (!added.IsSuccess)
                return FeatureObjectResultModel<AdminAddSpecificationAttributeOptionResponse>.Error(added.Messages);

            session.Store(attribute);
            session.Store(AdminAudit.AdminActionLog.Executed(
                cmd.UserId, Shared.CatalogAdminTools.AddSpecificationAttributeOption,
                attribute.Id.ToString(), $"added option '{cmd.Name}'"));

            return FeatureObjectResultModel<AdminAddSpecificationAttributeOptionResponse>.Ok(
                new AdminAddSpecificationAttributeOptionResponse { OptionId = added.Data });
        }
    }
}
