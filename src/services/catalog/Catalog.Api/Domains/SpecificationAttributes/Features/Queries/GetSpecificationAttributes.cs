namespace Catalog.Api.Domains.SpecificationAttributes.Features.Queries;

public static class GetSpecificationAttributes
{
    public record GetSpecificationAttributesQuery;

    public class SpecificationOptionResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int DisplayOrder { get; set; }
    }

    public class GetSpecificationAttributesResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public bool Filterable { get; set; }
        public int DisplayOrder { get; set; }
        public List<SpecificationOptionResponse> Options { get; set; } = [];
    }

    public class GetSpecificationAttributesQueryHandler
    {
        public async Task<FeatureListResultModel<GetSpecificationAttributesResponse>> Handle(
            GetSpecificationAttributesQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var attributes = await session.Query<SpecificationAttribute>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.DisplayOrder)
                .ToListAsync(ct);

            return FeatureListResultModel<GetSpecificationAttributesResponse>.Ok(
                attributes.Select(a => new GetSpecificationAttributesResponse
                {
                    Id = a.Id,
                    Name = a.Name,
                    Filterable = a.Filterable,
                    DisplayOrder = a.DisplayOrder,
                    Options = a.Options.OrderBy(o => o.DisplayOrder)
                        .Select(o => new SpecificationOptionResponse
                        { Id = o.Id, Name = o.Name, DisplayOrder = o.DisplayOrder }).ToList(),
                }).ToList());
        }
    }
}

public static class GetSpecificationAttributesQueryEndpoint
{
    public static RouteGroupBuilder GetSpecificationAttributesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus
                    .InvokeAsync<FeatureListResultModel<GetSpecificationAttributes.GetSpecificationAttributesResponse>>(
                        new GetSpecificationAttributes.GetSpecificationAttributesQuery());
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("GetSpecificationAttributes");
        return group;
    }
}
