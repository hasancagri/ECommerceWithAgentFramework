namespace CustomNopCommerce.Domains.TaxRates.Features.Queries;

/// <summary>Bir vergi kategorisinin oranlarını listeleyen read-slice'ı.</summary>
public static class ListTaxRatesByCategory
{
    public record ListTaxRatesByCategoryQuery(Guid TaxCategoryId);

    public class TaxRateItem
    {
        public Guid Id { get; set; }
        public Guid? CountryId { get; set; }
        public decimal Percentage { get; set; }
    }

    public class ListTaxRatesByCategoryQueryHandler
    {
        public async Task<FeatureListResultModel<TaxRateItem>> Handle(
            ListTaxRatesByCategoryQuery query, IQuerySession session, CancellationToken ct)
        {
            var rates = await session.Query<TaxRate>()
                .Where(r => r.TaxCategoryId == query.TaxCategoryId && !r.IsDeleted)
                .ToListAsync(ct);

            var items = rates.Select(r => new TaxRateItem
            {
                Id = r.Id,
                CountryId = r.CountryId,
                Percentage = r.Percentage,
            }).ToList();

            return FeatureListResultModel<TaxRateItem>.Ok(items);
        }
    }
}

public static class ListTaxRatesByCategoryQueryEndpoint
{
    public static RouteGroupBuilder ListTaxRatesByCategoryGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/by-category/{categoryId:guid}", async (Guid categoryId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListTaxRatesByCategory.TaxRateItem>>(
                    new ListTaxRatesByCategory.ListTaxRatesByCategoryQuery(categoryId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListTaxRatesByCategory");
        return group;
    }
}
