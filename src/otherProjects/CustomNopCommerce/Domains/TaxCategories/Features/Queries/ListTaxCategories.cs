namespace CustomNopCommerce.Domains.TaxCategories.Features.Queries;

/// <summary>Vergi kategorilerini sıralı listeleyen read-slice'ı.</summary>
public static class ListTaxCategories
{
    public record ListTaxCategoriesQuery;

    public class TaxCategoryItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public int DisplayOrder { get; set; }
    }

    public class ListTaxCategoriesQueryHandler
    {
        public async Task<FeatureListResultModel<TaxCategoryItem>> Handle(
            ListTaxCategoriesQuery query, IQuerySession session, CancellationToken ct)
        {
            var categories = await session.Query<TaxCategory>()
                .Where(c => !c.IsDeleted)
                .ToListAsync(ct);

            var items = categories
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new TaxCategoryItem { Id = c.Id, Name = c.Name, DisplayOrder = c.DisplayOrder })
                .ToList();

            return FeatureListResultModel<TaxCategoryItem>.Ok(items);
        }
    }
}

public static class ListTaxCategoriesQueryEndpoint
{
    public static RouteGroupBuilder ListTaxCategoriesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListTaxCategories.TaxCategoryItem>>(
                    new ListTaxCategories.ListTaxCategoriesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListTaxCategories");
        return group;
    }
}
