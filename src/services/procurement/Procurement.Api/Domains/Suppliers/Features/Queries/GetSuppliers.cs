namespace Procurement.Api.Domains.Suppliers.Features.Queries;

public static class GetSuppliers
{
    public record GetSuppliersQuery;

    public class GetSuppliersResponse
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public int Priority { get; set; }
        public int CategoryMappingCount { get; set; }
    }

    public class GetSuppliersQueryHandler
    {
        public async Task<FeatureListResultModel<GetSuppliersResponse>> Handle(
            GetSuppliersQuery query, IQuerySession session, CancellationToken ct)
        {
            var suppliers = await session.Query<Supplier>()
                .OrderBy(s => s.Priority)
                .ToListAsync(ct);

            return FeatureListResultModel<GetSuppliersResponse>.Ok(suppliers.Select(s => new GetSuppliersResponse
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                Priority = s.Priority,
                CategoryMappingCount = s.CategoryMappings.Count,
            }).ToList());
        }
    }
}