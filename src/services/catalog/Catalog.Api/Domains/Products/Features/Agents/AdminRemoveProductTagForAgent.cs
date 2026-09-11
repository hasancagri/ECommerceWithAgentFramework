namespace Catalog.Api.Domains.Products.Features.Agents;

// 074: admin üründen etiket çıkarma (agent yüzeyi) — REST RemoveTagFromProduct ikizi. İz: AdminActionLog.
public static class AdminRemoveProductTagForAgent
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminRemoveProductTagCommand(Guid UserId, Guid ProductId, Guid TagId);

    public class AdminRemoveProductTagResponse
    {
        public Guid ProductId { get; set; }
        public Guid TagId { get; set; }
    }

    [Transactional]
    public class AdminRemoveProductTagForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminRemoveProductTagResponse>> Handle(
            AdminRemoveProductTagCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, CatalogAdminTools.RemoveProductTag, cmd.ProductId.ToString(), "product not found"));
                return FeatureObjectResultModel<AdminRemoveProductTagResponse>.NotFound();
            }

            var remove = product.RemoveTag(cmd.TagId);
            if (!remove.IsSuccess)
                return FeatureObjectResultModel<AdminRemoveProductTagResponse>.Error(remove.Messages);

            session.Store(product);
            session.Store(AdminAudit.AdminActionLog.Executed(
                cmd.UserId, CatalogAdminTools.RemoveProductTag, product.Id.ToString(), $"Tag {cmd.TagId} removed"));
            return FeatureObjectResultModel<AdminRemoveProductTagResponse>.Ok(
                new AdminRemoveProductTagResponse { ProductId = product.Id, TagId = cmd.TagId });
        }
    }
}
