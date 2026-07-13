namespace Catalog.Api.Domains.Products.Features.Agent;

// Enrichment (002): urunun aciklamasini yalnizca bossa yazar (FR-005, idempotent).
public static class SetProductDescription
{
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record SetProductDescriptionCommand(Guid Id, string Description);

    public class SetProductDescriptionResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class SetProductDescriptionCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetProductDescriptionResponse>> Handle(
            SetProductDescriptionCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var product = await session.LoadAsync<Product>(cmd.Id, ct);
            if (product is null || product.IsDeleted)
                return FeatureObjectResultModel<SetProductDescriptionResponse>.NotFound();

            product.SetDescriptionIfEmpty(cmd.Description);
            session.Store(product);

            return FeatureObjectResultModel<SetProductDescriptionResponse>.Ok(
                new SetProductDescriptionResponse { Id = product.Id });
        }
    }
}