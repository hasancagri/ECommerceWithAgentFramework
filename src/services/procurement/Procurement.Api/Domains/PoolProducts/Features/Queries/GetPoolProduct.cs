namespace Procurement.Api.Domains.PoolProducts.Features.Queries;

// Havuz okuma penceresi (CLAUDE.md aggregate-REST kuralı): tekil barkod + durum filtreli liste.
public static class GetPoolProduct
{
    public record GetPoolProductQuery(string Barcode);

    public record GetPoolProductsQuery(PoolProductStatus? Status, int Page = 1, int PageSize = 50);

    public class PoolProductListingResponse
    {
        public Guid SupplierId { get; set; }
        public int SupplierPriority { get; set; }
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
        public BuyBoxDecision? PublishedBuyBox { get; set; }
        public bool NeedsEnrichment { get; set; }
        public List<PoolProductListingResponse> Listings { get; set; } = [];
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
                PublishedBuyBox = product.PublishedBuyBox,
                NeedsEnrichment = product.NeedsEnrichment,
                Listings = product.Listings.Select(l => new PoolProductListingResponse
                {
                    SupplierId = l.SupplierId,
                    SupplierPriority = l.SupplierPriority,
                    SupplierSku = l.SupplierSku,
                    Price = l.Price,
                    Stock = l.Stock,
                    IsDelisted = l.IsDelisted,
                    LastSeenUtc = l.LastSeenUtc,
                }).ToList(),
            };
}