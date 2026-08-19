namespace CustomNopCommerce.Domains.MessageTemplates.Features.Commands;

/// <summary>Yeni mesaj şablonu oluşturma write-slice'ı.</summary>
public static class CreateMessageTemplate
{
    public record CreateMessageTemplateCommand(string Name, string Subject, string Body, string? BccEmailAddresses);

    public class CreateMessageTemplateResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateMessageTemplateCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateMessageTemplateResponse>> Handle(
            CreateMessageTemplateCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateMessageTemplateResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = MessagingResourceConstants.TEMPLATE_NAME_REQUIRED });
            if (string.IsNullOrWhiteSpace(cmd.Subject))
                return FeatureObjectResultModel<CreateMessageTemplateResponse>.Error(new MessageItem
                { Property = nameof(cmd.Subject), Code = MessagingResourceConstants.TEMPLATE_SUBJECT_REQUIRED });

            var template = MessageTemplate.Create(cmd.Name, cmd.Subject, cmd.Body, cmd.BccEmailAddresses);
            session.Store(template);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateMessageTemplateResponse>.Ok(
                new CreateMessageTemplateResponse { Id = template.Id });
        }
    }
}

public static class CreateMessageTemplateCommandEndpoint
{
    public static RouteGroupBuilder CreateMessageTemplateGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateMessageTemplate.CreateMessageTemplateCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateMessageTemplate.CreateMessageTemplateResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateMessageTemplate");
        return group;
    }
}
