namespace Catalog.Api.Domains.Products.Features.Agents;

// 074: admin ürün ölçü ayarı (agent yüzeyi) — REST SetProductDimensions ikizi. İz: AdminActionLog.
public static class AdminSetProductDimensionsForAgent
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record AdminSetProductDimensionsCommand(
        Guid UserId, Guid ProductId, decimal Weight, decimal Length, decimal Width, decimal Height);

    public class AdminSetProductDimensionsResponse
    {
        public Guid ProductId { get; set; }
    }

    [Transactional]
    public class AdminSetProductDimensionsForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminSetProductDimensionsResponse>> Handle(
            AdminSetProductDimensionsCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.ProductId, ct);
            if (product is null || product.IsDeleted)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, CatalogAdminTools.SetProductDimensions, cmd.ProductId.ToString(), "product not found"));
                return FeatureObjectResultModel<AdminSetProductDimensionsResponse>.NotFound();
            }

            var dimensions = ProductDimensions.Create(cmd.Weight, cmd.Length, cmd.Width, cmd.Height);
            if (dimensions is null)
                return FeatureObjectResultModel<AdminSetProductDimensionsResponse>.Error(new MessageItem
                { Property = nameof(cmd.Weight), Code = CatalogResourceConstants.PRODUCT_DIMENSIONS_INVALID });

            var set = product.SetDimensions(dimensions);
            if (!set.IsSuccess)
                return FeatureObjectResultModel<AdminSetProductDimensionsResponse>.Error(set.Messages);

            session.Store(product);
            session.Store(AdminAudit.AdminActionLog.Executed(
                cmd.UserId, CatalogAdminTools.SetProductDimensions, product.Id.ToString(),
                $"Dimensions {cmd.Weight}/{cmd.Length}x{cmd.Width}x{cmd.Height}"));
            return FeatureObjectResultModel<AdminSetProductDimensionsResponse>.Ok(
                new AdminSetProductDimensionsResponse { ProductId = product.Id });
        }
    }
}
