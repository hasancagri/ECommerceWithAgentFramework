namespace CustomNopCommerce.Domains.Categories.Features.Queries;

/// <summary>Kategorileri listeleyen read-slice'ı (sıralamaya göre).</summary>
public static class ListCategories
{
    public record ListCategoriesQuery;

    public class ListCategoriesItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public Guid? ParentCategoryId { get; set; }
        public int DisplayOrder { get; set; }
        public bool Published { get; set; }
    }

    public class ListCategoriesQueryHandler
    {
        public async Task<FeatureListResultModel<ListCategoriesItem>> Handle(
            ListCategoriesQuery query, IQuerySession session, CancellationToken ct)
        {
            var categories = await session.Query<Category>()
                .Where(c => !c.IsDeleted)
                .ToListAsync(ct);

            var items = categories
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new ListCategoriesItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    ParentCategoryId = c.ParentCategoryId,
                    DisplayOrder = c.DisplayOrder,
                    Published = c.Published,
                }).ToList();

            return FeatureListResultModel<ListCategoriesItem>.Ok(items);
        }
    }
}

public static class ListCategoriesQueryEndpoint
{
    public static RouteGroupBuilder ListCategoriesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListCategories.ListCategoriesItem>>(
                    new ListCategories.ListCategoriesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListCategories");
        return group;
    }
}
