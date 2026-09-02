namespace Catalog.Api.Domains.Publishers.Features.Queries;

// 058: admin düzenleme formu yayınevi seçim listesi (GetAuthors deseniyle aynı).
public static class GetPublishers
{
    public record GetPublishersQuery;

    public class GetPublishersResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
    }

    public class GetPublishersQueryHandler
    {
        public async Task<FeatureListResultModel<GetPublishersResponse>> Handle(
            GetPublishersQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var publishers = await session.Query<Publisher>()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.Name)
                .ToListAsync(ct);

            return FeatureListResultModel<GetPublishersResponse>.Ok(
                publishers.Select(x => new GetPublishersResponse { Id = x.Id, Name = x.Name }).ToList());
        }
    }
}

public static class GetPublishersQueryEndpoint
{
    public static RouteGroupBuilder GetPublishersGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetPublishers.GetPublishersResponse>>(
                    new GetPublishers.GetPublishersQuery());
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("GetPublishers");
        return group;
    }
}