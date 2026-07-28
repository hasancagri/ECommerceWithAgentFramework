namespace Catalog.Api.Domains.Brands.Features.Queries;

public static class GetAllBrands
{
    [Cached("catalog-products", 60)]
    public record GetAllBrandsQuery();

    public class BrandResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
    }

    public class GetAllBrandsQueryHandler
    {
        public async Task<FeatureObjectResultModel<List<BrandResponse>>> Handle(
            GetAllBrandsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var brands = await session.Query<Brand>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .Select(x => new BrandResponse { Id = x.Id, Name = x.Name })
                .ToListAsync(ct);

            return FeatureObjectResultModel<List<BrandResponse>>.Ok(brands.ToList());
        }
    }
}

public static class GetAllBrandsQueryEndpoint
{
    public static RouteGroupBuilder GetAllBrandsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/brands", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetAllBrands.BrandResponse>>>(
                    new GetAllBrands.GetAllBrandsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("GetAllBrands")
            .AllowAnonymous();
        return group;
    }
}