namespace Catalog.Api.Domains.Products.Features.Agents;

// 074: admin ürün SEO ayarı (agent yüzeyi) — REST SetProductSeo ikizi. İz: AdminActionLog.
public static class AdminSetProductSeoForAgent
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminSetProductSeoCommand(
        Guid UserId, Guid ProductId, string? MetaTitle, string? MetaKeywords, string? MetaDescription);

    public class AdminSetProductSeoResponse
    {
        public Guid ProductId { get; set; }
    }

    [Transactional]
    public class AdminSetProductSeoForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminSetProductSeoResponse>> Handle(
            AdminSetProductSeoCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, CatalogAdminTools.SetProductSeo, cmd.ProductId.ToString(), "product not found"));
                return FeatureObjectResultModel<AdminSetProductSeoResponse>.NotFound();
            }

            var set = product.SetSeo(SeoMetadata.Create(cmd.MetaTitle, cmd.MetaKeywords, cmd.MetaDescription));
            if (!set.IsSuccess)
                return FeatureObjectResultModel<AdminSetProductSeoResponse>.Error(set.Messages);

            session.Store(product);
            session.Store(AdminAudit.AdminActionLog.Executed(
                cmd.UserId, CatalogAdminTools.SetProductSeo, product.Id.ToString(), "SEO updated"));
            return FeatureObjectResultModel<AdminSetProductSeoResponse>.Ok(
                new AdminSetProductSeoResponse { ProductId = product.Id });
        }
    }
}
