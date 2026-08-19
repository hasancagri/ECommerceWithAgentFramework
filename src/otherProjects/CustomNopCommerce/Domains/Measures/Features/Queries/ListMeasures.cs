namespace CustomNopCommerce.Domains.Measures.Features.Queries;

/// <summary>Ölçü birimlerini (türe göre süzülebilir) listeleyen read-slice'ı.</summary>
public static class ListMeasures
{
    public record ListMeasuresQuery(MeasureType Type);

    public class MeasureItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string SystemKeyword { get; set; } = default!;
        public decimal Ratio { get; set; }
    }

    public class ListMeasuresQueryHandler
    {
        public async Task<FeatureListResultModel<MeasureItem>> Handle(
            ListMeasuresQuery query, IQuerySession session, CancellationToken ct)
        {
            var measures = await session.Query<Measure>()
                .Where(m => m.Type == query.Type && !m.IsDeleted)
                .ToListAsync(ct);

            var items = measures
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new MeasureItem
                {
                    Id = m.Id,
                    Name = m.Name,
                    SystemKeyword = m.SystemKeyword,
                    Ratio = m.Ratio,
                }).ToList();

            return FeatureListResultModel<MeasureItem>.Ok(items);
        }
    }
}

public static class ListMeasuresQueryEndpoint
{
    public static RouteGroupBuilder ListMeasuresGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (MeasureType type, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListMeasures.MeasureItem>>(
                    new ListMeasures.ListMeasuresQuery(type));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListMeasures");
        return group;
    }
}
