namespace Catalog.Api.Domains.ProductTags.Features.Agents;

// 070 parite: etiket OLUŞTURMA (agent yüzeyi) — CreateProductTag İKİZİ (bilinçli tekrar). Etiket dış
// kontrat taşımaz (Storefront/event yok) — yalnız Catalog içi kayıt. İz: AdminActionLog (Executed).
public static class AdminCreateProductTagForAgent
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminCreateProductTagCommand(Guid UserId, string Name);

    public class AdminCreateProductTagResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    [Transactional]
    public class AdminCreateProductTagForAgentCommandHandler
    {
        public Task<FeatureObjectResultModel<AdminCreateProductTagResponse>> Handle(
            AdminCreateProductTagCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Ad zorunluluğu handler'da (factory düz aggregate döner — ProductTag.Create notu).
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return Task.FromResult(FeatureObjectResultModel<AdminCreateProductTagResponse>.Error(new MessageItem
                {
                    Property = nameof(cmd.Name),
                    Code = CatalogResourceConstants.TAG_NAME_REQUIRED
                }));

            var tag = ProductTag.Create(cmd.Name);
            session.Store(tag);

            session.Store(AdminAudit.AdminActionLog.Executed(
                cmd.UserId, Shared.CatalogAdminTools.CreateProductTag, tag.Id.ToString(), $"created tag '{tag.Name}'"));

            return Task.FromResult(FeatureObjectResultModel<AdminCreateProductTagResponse>.Ok(
                new AdminCreateProductTagResponse { Id = tag.Id, Name = tag.Name }));
        }
    }
}
