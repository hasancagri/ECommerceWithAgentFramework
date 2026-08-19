namespace CustomNopCommerce.Domains.NewsLetterSubscriptions.Features.Queries;

/// <summary>Aktif bülten abonelerini listeleyen read-slice'ı.</summary>
public static class ListSubscriptions
{
    public record ListSubscriptionsQuery;

    public class SubscriptionItem
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = default!;
    }

    public class ListSubscriptionsQueryHandler
    {
        public async Task<FeatureListResultModel<SubscriptionItem>> Handle(
            ListSubscriptionsQuery query, IQuerySession session, CancellationToken ct)
        {
            var subscriptions = await session.Query<NewsLetterSubscription>()
                .Where(s => s.Active && !s.IsDeleted)
                .ToListAsync(ct);

            var items = subscriptions.Select(s => new SubscriptionItem { Id = s.Id, Email = s.Email }).ToList();
            return FeatureListResultModel<SubscriptionItem>.Ok(items);
        }
    }
}

public static class ListSubscriptionsQueryEndpoint
{
    public static RouteGroupBuilder ListSubscriptionsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListSubscriptions.SubscriptionItem>>(
                    new ListSubscriptions.ListSubscriptionsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListSubscriptions");
        return group;
    }
}
