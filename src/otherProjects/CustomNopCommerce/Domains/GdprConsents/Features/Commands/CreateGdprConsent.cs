namespace CustomNopCommerce.Domains.GdprConsents.Features.Commands;

/// <summary>Yeni GDPR rıza tanımı oluşturma write-slice'ı.</summary>
public static class CreateGdprConsent
{
    public record CreateGdprConsentCommand(
        string Message,
        bool IsRequired,
        string? RequiredMessage,
        bool DisplayDuringRegistration,
        bool DisplayOnCustomerInfoPage,
        int DisplayOrder);

    public class CreateGdprConsentResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateGdprConsentCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateGdprConsentResponse>> Handle(
            CreateGdprConsentCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Message))
                return FeatureObjectResultModel<CreateGdprConsentResponse>.Error(new MessageItem
                { Property = nameof(cmd.Message), Code = GdprResourceConstants.CONSENT_MESSAGE_REQUIRED });

            var consent = GdprConsent.Create(cmd.Message, cmd.IsRequired, cmd.RequiredMessage,
                cmd.DisplayDuringRegistration, cmd.DisplayOnCustomerInfoPage, cmd.DisplayOrder);
            session.Store(consent);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateGdprConsentResponse>.Ok(
                new CreateGdprConsentResponse { Id = consent.Id });
        }
    }
}

public static class CreateGdprConsentCommandEndpoint
{
    public static RouteGroupBuilder CreateGdprConsentGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateGdprConsent.CreateGdprConsentCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateGdprConsent.CreateGdprConsentResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateGdprConsent");
        return group;
    }
}
