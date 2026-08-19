namespace CustomNopCommerce.Domains.UrlRecords.Features.Queries;

/// <summary>Aktif bir slug'ı hangi varlığa (EntityName + EntityId) işaret ettiğine çözen read-slice'ı.
/// Gelen istek URL'inden slug'ı varlığa çözmek için kullanılır (routing).</summary>
public static class GetBySlug
{
    public record GetBySlugQuery(string Slug);

    public class GetBySlugResponse
    {
        public Guid EntityId { get; set; }
        public string EntityName { get; set; } = default!;
        public string Slug { get; set; } = default!;
    }

    public class GetBySlugQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetBySlugResponse>> Handle(
            GetBySlugQuery query, IQuerySession session, CancellationToken ct)
        {
            var normalized = query.Slug.Trim().ToLowerInvariant().Replace(' ', '-');
            var record = await session.Query<UrlRecord>()
                .Where(u => u.Slug == normalized && u.IsActive && !u.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (record is null)
                return FeatureObjectResultModel<GetBySlugResponse>.NotFound();

            return FeatureObjectResultModel<GetBySlugResponse>.Ok(new GetBySlugResponse
            {
                EntityId = record.EntityId,
                EntityName = record.EntityName,
                Slug = record.Slug,
            });
        }
    }
}

public static class GetBySlugQueryEndpoint
{
    public static RouteGroupBuilder GetBySlugGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/resolve/{slug}", async (string slug, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetBySlug.GetBySlugResponse>>(
                    new GetBySlug.GetBySlugQuery(slug));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetBySlug");
        return group;
    }
}
