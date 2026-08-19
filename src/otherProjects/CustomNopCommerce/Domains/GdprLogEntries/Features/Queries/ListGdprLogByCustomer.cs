namespace CustomNopCommerce.Domains.GdprLogEntries.Features.Queries;

/// <summary>Bir müşterinin GDPR denetim kayıtlarını (yeniden eskiye) listeleyen read-slice'ı.</summary>
public static class ListGdprLogByCustomer
{
    public record ListGdprLogByCustomerQuery(Guid CustomerId);

    public class LogItem
    {
        public Guid Id { get; set; }
        public GdprRequestType RequestType { get; set; }
        public string? RequestDetails { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class ListGdprLogByCustomerQueryHandler
    {
        public async Task<FeatureListResultModel<LogItem>> Handle(
            ListGdprLogByCustomerQuery query, IQuerySession session, CancellationToken ct)
        {
            var entries = await session.Query<GdprLogEntry>()
                .Where(e => e.CustomerId == query.CustomerId && !e.IsDeleted)
                .ToListAsync(ct);

            var items = entries
                .OrderByDescending(e => e.CreatedTime)
                .Select(e => new LogItem
                {
                    Id = e.Id,
                    RequestType = e.RequestType,
                    RequestDetails = e.RequestDetails,
                    CreatedAtUtc = e.CreatedTime,
                }).ToList();

            return FeatureListResultModel<LogItem>.Ok(items);
        }
    }
}

public static class ListGdprLogByCustomerQueryEndpoint
{
    public static RouteGroupBuilder ListGdprLogByCustomerGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/by-customer/{customerId:guid}", async (Guid customerId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListGdprLogByCustomer.LogItem>>(
                    new ListGdprLogByCustomer.ListGdprLogByCustomerQuery(customerId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListGdprLogByCustomer");
        return group;
    }
}
