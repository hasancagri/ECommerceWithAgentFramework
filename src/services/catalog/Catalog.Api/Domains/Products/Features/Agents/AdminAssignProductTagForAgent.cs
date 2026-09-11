namespace Catalog.Api.Domains.Products.Features.Agents;

// 074: admin ürüne etiket atama (agent yüzeyi) — REST AssignTagToProduct ikizi. İz: AdminActionLog.
public static class AdminAssignProductTagForAgent
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminAssignProductTagCommand(Guid UserId, Guid ProductId, Guid TagId);

    public class AdminAssignProductTagResponse
    {
        public Guid ProductId { get; set; }
        public Guid TagId { get; set; }
    }

    [Transactional]
    public class AdminAssignProductTagForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminAssignProductTagResponse>> Handle(
            AdminAssignProductTagCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, CatalogAdminTools.AssignProductTag, cmd.ProductId.ToString(), "product not found"));
                return FeatureObjectResultModel<AdminAssignProductTagResponse>.NotFound();
            }

            var tag = await session.LoadAsync<ProductTags.ProductTag>(cmd.TagId, ct);
            if (tag is null || tag.IsDeleted)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, CatalogAdminTools.AssignProductTag, cmd.ProductId.ToString(), "tag not found"));
                return FeatureObjectResultModel<AdminAssignProductTagResponse>.Error(new MessageItem
                { Property = nameof(cmd.TagId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });
            }

            var add = product.AddTag(cmd.TagId);
            if (!add.IsSuccess)
                return FeatureObjectResultModel<AdminAssignProductTagResponse>.Error(add.Messages);

            session.Store(product);
            session.Store(AdminAudit.AdminActionLog.Executed(
                cmd.UserId, CatalogAdminTools.AssignProductTag, product.Id.ToString(), $"Tag {cmd.TagId} assigned"));
            return FeatureObjectResultModel<AdminAssignProductTagResponse>.Ok(
                new AdminAssignProductTagResponse { ProductId = product.Id, TagId = cmd.TagId });
        }
    }
}
