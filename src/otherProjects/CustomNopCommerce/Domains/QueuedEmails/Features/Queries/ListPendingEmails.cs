namespace CustomNopCommerce.Domains.QueuedEmails.Features.Queries;

/// <summary>Henüz gönderilmemiş (bekleyen) e-postaları listeleyen read-slice'ı.</summary>
public static class ListPendingEmails
{
    public record ListPendingEmailsQuery;

    public class PendingEmailItem
    {
        public Guid Id { get; set; }
        public string To { get; set; } = default!;
        public string Subject { get; set; } = default!;
        public QueuedEmailPriority Priority { get; set; }
        public int SentTries { get; set; }
    }

    public class ListPendingEmailsQueryHandler
    {
        public async Task<FeatureListResultModel<PendingEmailItem>> Handle(
            ListPendingEmailsQuery query, IQuerySession session, CancellationToken ct)
        {
            var emails = await session.Query<QueuedEmail>()
                .Where(e => e.SentOnUtc == null && !e.IsDeleted)
                .ToListAsync(ct);

            var items = emails
                .OrderByDescending(e => e.Priority)
                .Select(e => new PendingEmailItem
                {
                    Id = e.Id,
                    To = e.To,
                    Subject = e.Subject,
                    Priority = e.Priority,
                    SentTries = e.SentTries,
                }).ToList();

            return FeatureListResultModel<PendingEmailItem>.Ok(items);
        }
    }
}

public static class ListPendingEmailsQueryEndpoint
{
    public static RouteGroupBuilder ListPendingEmailsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/pending", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListPendingEmails.PendingEmailItem>>(
                    new ListPendingEmails.ListPendingEmailsQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListPendingEmails");
        return group;
    }
}
