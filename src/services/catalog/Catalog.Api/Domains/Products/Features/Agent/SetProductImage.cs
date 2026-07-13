namespace Catalog.Api.Domains.Products.Features.Agent;

// Enrichment (002): urunun gorsel URL'ini yalnizca bossa yazar (FR-005). Yazinca
// RecalculateCompleteness calisir; urun tamsa (aktifse) satisa cikar.
public static class SetProductImage
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record SetProductImageCommand(Guid Id, string ImageUrl);

    public class SetProductImageResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class SetProductImageCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetProductImageResponse>> Handle(
            SetProductImageCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<SetProductImageResponse>.NotFound();

            product.SetImageUrlIfEmpty(cmd.ImageUrl);
            session.Store(product);

            return FeatureObjectResultModel<SetProductImageResponse>.Ok(
                new SetProductImageResponse { Id = product.Id });
        }
    }
}