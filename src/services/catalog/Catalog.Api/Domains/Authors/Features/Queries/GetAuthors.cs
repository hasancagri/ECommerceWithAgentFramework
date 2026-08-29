namespace Catalog.Api.Domains.Authors.Features.Queries;

public static class GetAuthors
{
    public record GetAuthorsQuery;

    public class GetAuthorsResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    public class GetAuthorsQueryHandler
    {
        public async Task<FeatureListResultModel<GetAuthorsResponse>> Handle(
            GetAuthorsQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var authors = await session.Query<Author>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            return FeatureListResultModel<GetAuthorsResponse>.Ok(
                authors.Select(x => new GetAuthorsResponse { Id = x.Id, Name = x.Name }).ToList());
        }
    }
}

public static class GetAuthorsQueryEndpoint
{
    public static RouteGroupBuilder GetAuthorsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetAuthors.GetAuthorsResponse>>(
                    new GetAuthors.GetAuthorsQuery());
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("GetAuthors");
        return group;
    }
}