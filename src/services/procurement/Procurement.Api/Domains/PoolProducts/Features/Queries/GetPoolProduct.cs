namespace Procurement.Api.Domains.PoolProducts.Features.Queries;

// Havuz okuma penceresi (CLAUDE.md aggregate-REST kuralı): tekil barkod + durum filtreli liste.
public static class GetPoolProduct
{
    public record GetPoolProductQuery(string Barcode);

    public record GetPoolProductsQuery(PoolProductStatus? Status, int Page = 1, int PageSize = 50);

    public class PoolProductListingResponse
    {
        public Guid SupplierId { get; set; }
        public string SupplierSku { get; set; } = default!;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public bool IsDelisted { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }

    public class GetPoolProductResponse
    {
        public string Barcode { get; set; } = default!;
        public string Status { get; set; } = default!;
        public CanonicalContent? Canonical { get; set; }
        // 047: buy-box söküldü — güncel teklif (fiyat/stok) tek listing'ten.
        public decimal CurrentPrice { get; set; }
        public int CurrentStock { get; set; }
        public bool NeedsEnrichment { get; set; }
        // 047: barkod-başı tek tedarikçi (null = henüz listing yok).
        public PoolProductListingResponse? Listing { get; set; }
    }

    public class GetPoolProductQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetPoolProductResponse>> Handle(
            GetPoolProductQuery query, IQuerySession session, CancellationToken ct)
        {
            var product = await session.LoadAsync<PoolProduct>(query.Barcode, ct);
            return FeatureObjectResultModel<GetPoolProductResponse>.Ok(ToResponse(product));
        }
    }

    public class GetPoolProductsQueryHandler
    {
        public async Task<FeatureListResultModel<GetPoolProductResponse>> Handle(
            GetPoolProductsQuery query, IQuerySession session, CancellationToken ct)
        {
            var q = session.Query<PoolProduct>().AsQueryable();
            if (query.Status is not null)
                q = q.Where(p => p.Status == query.Status);

            var products = await q.OrderBy(p => p.Barcode)
                .Skip((Math.Max(query.Page, 1) - 1) * query.PageSize)
                .Take(Math.Clamp(query.PageSize, 1, 200))
                .ToListAsync(ct);

            return FeatureListResultModel<GetPoolProductResponse>.Ok(
                products.Select(p => ToResponse(p)!).ToList());
        }
    }

    private static GetPoolProductResponse? ToResponse(PoolProduct? product)
        => product is null
            ? null
            : new GetPoolProductResponse
            {
                Barcode = product.Barcode,
                Status = product.Status.ToString(),
                Canonical = product.Canonical,
                CurrentPrice = product.CurrentOffer.Price,
                CurrentStock = product.CurrentOffer.Stock,
                NeedsEnrichment = product.NeedsEnrichment,
                Listing = product.Listing is null ? null : new PoolProductListingResponse
                {
                    SupplierId = product.Listing.SupplierId,
                    SupplierSku = product.Listing.SupplierSku,
                    Price = product.Listing.Price,
                    Stock = product.Listing.Stock,
                    IsDelisted = product.Listing.IsDelisted,
                    LastSeenUtc = product.Listing.LastSeenUtc,
                },
            };
}