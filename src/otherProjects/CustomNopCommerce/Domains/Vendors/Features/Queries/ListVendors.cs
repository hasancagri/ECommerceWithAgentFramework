namespace CustomNopCommerce.Domains.Vendors.Features.Queries;

/// <summary>Satıcıları sıralı listeleyen read-slice'ı.</summary>
public static class ListVendors
{
    public record ListVendorsQuery;

    public class VendorItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public bool IsActive { get; set; }
        public int NoteCount { get; set; }
    }

    public class ListVendorsQueryHandler
    {
        public async Task<FeatureListResultModel<VendorItem>> Handle(
            ListVendorsQuery query, IQuerySession session, CancellationToken ct)
        {
            var vendors = await session.Query<Vendor>()
                .Where(v => !v.IsDeleted)
                .ToListAsync(ct);

            var items = vendors
                .OrderBy(v => v.DisplayOrder)
                .Select(v => new VendorItem
                {
                    Id = v.Id,
                    Name = v.Name,
                    Email = v.Email,
                    IsActive = v.IsActive,
                    NoteCount = v.Notes.Count,
                }).ToList();

            return FeatureListResultModel<VendorItem>.Ok(items);
        }
    }
}

public static class ListVendorsQueryEndpoint
{
    public static RouteGroupBuilder ListVendorsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListVendors.VendorItem>>(
                    new ListVendors.ListVendorsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListVendors");
        return group;
    }
}
