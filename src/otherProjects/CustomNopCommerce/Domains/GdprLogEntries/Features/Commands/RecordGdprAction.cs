namespace CustomNopCommerce.Domains.GdprLogEntries.Features.Commands;

/// <summary>Bir GDPR olayını denetim kaydına yazma write-slice'ı (append-only — kayıt sonradan değişmez).</summary>
public static class RecordGdprAction
{
    public record RecordGdprActionCommand(
        Guid CustomerId,
        Guid? ConsentId,
        GdprRequestType RequestType,
        string? RequestDetails,
        string? CustomerInfo);

    public class RecordGdprActionResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class RecordGdprActionCommandHandler
    {
        public async Task<FeatureObjectResultModel<RecordGdprActionResponse>> Handle(
            RecordGdprActionCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var entry = GdprLogEntry.Create(cmd.CustomerId, cmd.ConsentId, cmd.RequestType,
                cmd.RequestDetails, cmd.CustomerInfo);
            session.Store(entry);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<RecordGdprActionResponse>.Ok(
                new RecordGdprActionResponse { Id = entry.Id });
        }
    }
}

public static class RecordGdprActionCommandEndpoint
{
    public static RouteGroupBuilder RecordGdprActionGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] RecordGdprAction.RecordGdprActionCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<RecordGdprAction.RecordGdprActionResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("RecordGdprAction");
        return group;
    }
}
