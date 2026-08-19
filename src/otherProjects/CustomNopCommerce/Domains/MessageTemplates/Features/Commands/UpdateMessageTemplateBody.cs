namespace CustomNopCommerce.Domains.MessageTemplates.Features.Commands;

/// <summary>Şablonun konu + gövdesini güncelleyen write-slice'ı.</summary>
public static class UpdateMessageTemplateBody
{
    public record UpdateMessageTemplateBodyCommand(Guid Id, string Subject, string Body);

    [Transactional]
    public class UpdateMessageTemplateBodyCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            UpdateMessageTemplateBodyCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var template = await session.LoadAsync<MessageTemplate>(cmd.Id, ct);
            if (template is null || template.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = template.UpdateContent(cmd.Subject, cmd.Body);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(template);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class UpdateMessageTemplateBodyCommandEndpoint
{
    public static RouteGroupBuilder UpdateMessageTemplateBodyGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", async (Guid id,
            [FromBody] UpdateMessageTemplateBody.UpdateMessageTemplateBodyCommand body, IMessageBus bus) =>
            {
                var cmd = body with { Id = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("UpdateMessageTemplateBody");
        return group;
    }
}
