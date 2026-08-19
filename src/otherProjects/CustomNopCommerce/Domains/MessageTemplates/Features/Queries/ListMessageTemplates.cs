namespace CustomNopCommerce.Domains.MessageTemplates.Features.Queries;

/// <summary>Mesaj şablonlarını listeleyen read-slice'ı.</summary>
public static class ListMessageTemplates
{
    public record ListMessageTemplatesQuery;

    public class TemplateItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Subject { get; set; } = default!;
        public bool IsActive { get; set; }
    }

    public class ListMessageTemplatesQueryHandler
    {
        public async Task<FeatureListResultModel<TemplateItem>> Handle(
            ListMessageTemplatesQuery query, IQuerySession session, CancellationToken ct)
        {
            var templates = await session.Query<MessageTemplate>()
                .Where(t => !t.IsDeleted)
                .ToListAsync(ct);

            var items = templates.Select(t => new TemplateItem
            {
                Id = t.Id,
                Name = t.Name,
                Subject = t.Subject,
                IsActive = t.IsActive,
            }).ToList();

            return FeatureListResultModel<TemplateItem>.Ok(items);
        }
    }
}

public static class ListMessageTemplatesQueryEndpoint
{
    public static RouteGroupBuilder ListMessageTemplatesGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListMessageTemplates.TemplateItem>>(
                    new ListMessageTemplates.ListMessageTemplatesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListMessageTemplates");
        return group;
    }
}
