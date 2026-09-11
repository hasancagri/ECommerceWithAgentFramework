namespace Catalog.Api.Domains.ProductTags.Features.Agents;

// 070 parite: etiket ADI değiştirme (agent yüzeyi) — RenameProductTag İKİZİ (bilinçli tekrar).
// İz: AdminActionLog; bulunamadı da iz bırakır (Rejected satırı) + NotFound döner.
public static class AdminRenameProductTagForAgent
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminRenameProductTagCommand(Guid UserId, Guid TagId, string Name);

    public class AdminRenameProductTagResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    [Transactional]
    public class AdminRenameProductTagForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminRenameProductTagResponse>> Handle(
            AdminRenameProductTagCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var tag = await session.LoadAsync<ProductTag>(cmd.TagId, ct);
            if (tag is null || tag.IsDeleted)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, Shared.CatalogAdminTools.RenameProductTag, cmd.TagId.ToString(), "tag not found"));
                return FeatureObjectResultModel<AdminRenameProductTagResponse>.NotFound();
            }

            var rename = tag.Rename(cmd.Name);
            if (!rename.IsSuccess)
                return FeatureObjectResultModel<AdminRenameProductTagResponse>.Error(rename.Messages);

            session.Store(tag);

            session.Store(AdminAudit.AdminActionLog.Executed(
                cmd.UserId, Shared.CatalogAdminTools.RenameProductTag, tag.Id.ToString(), $"renamed tag to '{tag.Name}'"));

            return FeatureObjectResultModel<AdminRenameProductTagResponse>.Ok(
                new AdminRenameProductTagResponse { Id = tag.Id, Name = tag.Name });
        }
    }
}
