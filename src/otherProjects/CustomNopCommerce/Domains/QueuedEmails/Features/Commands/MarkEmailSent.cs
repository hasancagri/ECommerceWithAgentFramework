namespace CustomNopCommerce.Domains.QueuedEmails.Features.Commands;

/// <summary>Kuyruktaki e-postayı gönderildi işaretleyen write-slice'ı. Zaten gönderilmişse reddedilir (invariant).</summary>
public static class MarkEmailSent
{
    public record MarkEmailSentCommand(Guid Id);

    [Transactional]
    public class MarkEmailSentCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            MarkEmailSentCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var email = await session.LoadAsync<QueuedEmail>(cmd.Id, ct);
            if (email is null || email.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = email.MarkSent(DateTime.UtcNow);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(email);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class MarkEmailSentCommandEndpoint
{
    public static RouteGroupBuilder MarkEmailSentGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/mark-sent", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(new MarkEmailSent.MarkEmailSentCommand(id));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("MarkEmailSent");
        return group;
    }
}
