namespace CustomNopCommerce.Domains.QueuedEmails.Features.Commands;

/// <summary>Bir e-postayı gönderim kuyruğuna alma write-slice'ı.</summary>
public static class QueueEmail
{
    public record QueueEmailCommand(
        string To,
        string? ToName,
        string Subject,
        string Body,
        QueuedEmailPriority Priority,
        DateTime? DontSendBeforeUtc);

    public class QueueEmailResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class QueueEmailCommandHandler
    {
        public async Task<FeatureObjectResultModel<QueueEmailResponse>> Handle(
            QueueEmailCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.To))
                return FeatureObjectResultModel<QueueEmailResponse>.Error(new MessageItem
                { Property = nameof(cmd.To), Code = MessagingResourceConstants.EMAIL_RECIPIENT_REQUIRED });

            var email = QueuedEmail.Create(cmd.To, cmd.ToName, cmd.Subject, cmd.Body, cmd.Priority, cmd.DontSendBeforeUtc);
            session.Store(email);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<QueueEmailResponse>.Ok(new QueueEmailResponse { Id = email.Id });
        }
    }
}

public static class QueueEmailCommandEndpoint
{
    public static RouteGroupBuilder QueueEmailGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] QueueEmail.QueueEmailCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<QueueEmail.QueueEmailResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("QueueEmail");
        return group;
    }
}
