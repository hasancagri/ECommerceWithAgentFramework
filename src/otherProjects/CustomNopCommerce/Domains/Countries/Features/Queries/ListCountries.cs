namespace CustomNopCommerce.Domains.Countries.Features.Queries;

/// <summary>Ülkeleri (il sayısıyla) sıralı listeleyen read-slice'ı.</summary>
public static class ListCountries
{
    public record ListCountriesQuery;

    public class CountryItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? TwoLetterIsoCode { get; set; }
        public bool Published { get; set; }
        public int StateCount { get; set; }
    }

    public class ListCountriesQueryHandler
    {
        public async Task<FeatureListResultModel<CountryItem>> Handle(
            ListCountriesQuery query, IQuerySession session, CancellationToken ct)
        {
            var countries = await session.Query<Country>()
                .Where(c => !c.IsDeleted)
                .ToListAsync(ct);

            var items = countries
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new CountryItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    TwoLetterIsoCode = c.TwoLetterIsoCode,
                    Published = c.Published,
                    StateCount = c.States.Count,
                }).ToList();

            return FeatureListResultModel<CountryItem>.Ok(items);
        }
    }
}

public static class ListCountriesQueryEndpoint
{
    public static RouteGroupBuilder ListCountriesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListCountries.CountryItem>>(
                    new ListCountries.ListCountriesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListCountries");
        return group;
    }
}
