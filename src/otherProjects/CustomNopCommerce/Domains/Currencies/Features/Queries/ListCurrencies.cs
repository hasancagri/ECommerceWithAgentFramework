namespace CustomNopCommerce.Domains.Currencies.Features.Queries;

/// <summary>Para birimlerini sıralı listeleyen read-slice'ı.</summary>
public static class ListCurrencies
{
    public record ListCurrenciesQuery;

    public class CurrencyItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string CurrencyCode { get; set; } = default!;
        public decimal Rate { get; set; }
        public bool Published { get; set; }
    }

    public class ListCurrenciesQueryHandler
    {
        public async Task<FeatureListResultModel<CurrencyItem>> Handle(
            ListCurrenciesQuery query, IQuerySession session, CancellationToken ct)
        {
            var currencies = await session.Query<Currency>()
                .Where(c => !c.IsDeleted)
                .ToListAsync(ct);

            var items = currencies
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new CurrencyItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    CurrencyCode = c.CurrencyCode,
                    Rate = c.Rate,
                    Published = c.Published,
                }).ToList();

            return FeatureListResultModel<CurrencyItem>.Ok(items);
        }
    }
}

public static class ListCurrenciesQueryEndpoint
{
    public static RouteGroupBuilder ListCurrenciesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListCurrencies.CurrencyItem>>(
                    new ListCurrencies.ListCurrenciesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListCurrencies");
        return group;
    }
}
