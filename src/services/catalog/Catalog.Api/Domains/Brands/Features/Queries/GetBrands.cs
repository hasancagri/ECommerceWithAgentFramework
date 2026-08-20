namespace Catalog.Api.Domains.Brands.Features.Queries;

public static class GetBrands
{
    public record GetBrandsQuery;

    public class GetBrandsResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    public class GetBrandsQueryHandler
    {
        public async Task<FeatureListResultModel<GetBrandsResponse>> Handle(
            GetBrandsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var brands = await session.Query<Brand>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            return FeatureListResultModel<GetBrandsResponse>.Ok(
                brands.Select(x => new GetBrandsResponse { Id = x.Id, Name = x.Name }).ToList());
        }
    }
}

public static class GetBrandsQueryEndpoint
{
    public static RouteGroupBuilder GetBrandsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetBrands.GetBrandsResponse>>(
                    new GetBrands.GetBrandsQuery());
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("GetBrands");
        return group;
    }
}