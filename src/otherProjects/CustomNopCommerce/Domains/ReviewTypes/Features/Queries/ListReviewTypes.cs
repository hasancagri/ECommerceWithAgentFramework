namespace CustomNopCommerce.Domains.ReviewTypes.Features.Queries;

/// <summary>Yorum kriterlerini sıralı listeleyen read-slice'ı.</summary>
public static class ListReviewTypes
{
    public record ListReviewTypesQuery;

    public class ReviewTypeItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int DisplayOrder { get; set; }
        public bool IsRequired { get; set; }
    }

    public class ListReviewTypesQueryHandler
    {
        public async Task<FeatureListResultModel<ReviewTypeItem>> Handle(
            ListReviewTypesQuery query, IQuerySession session, CancellationToken ct)
        {
            var types = await session.Query<ReviewType>()
                .Where(t => !t.IsDeleted)
                .ToListAsync(ct);

            var items = types
                .OrderBy(t => t.DisplayOrder)
                .Select(t => new ReviewTypeItem
                {
                    Id = t.Id,
                    Name = t.Name,
                    DisplayOrder = t.DisplayOrder,
                    IsRequired = t.IsRequired,
                }).ToList();

            return FeatureListResultModel<ReviewTypeItem>.Ok(items);
        }
    }
}

public static class ListReviewTypesQueryEndpoint
{
    public static RouteGroupBuilder ListReviewTypesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListReviewTypes.ReviewTypeItem>>(
                    new ListReviewTypes.ListReviewTypesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListReviewTypes");
        return group;
    }
}
