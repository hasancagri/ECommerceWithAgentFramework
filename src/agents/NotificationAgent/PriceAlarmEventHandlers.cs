namespace NotificationAgent;

// 060: akisin sahibi — PriceAlarmTriggered tuket: email bos → gonderimsiz iz; degilse MailAgent
// maili yazip gonderir (her LLM/MCP/SMTP hatasi NotificationException → retry 10s/30s/60s → error queue).
// Cascade return: donen NotificationSent, Wolverine PublishMessage kurali ile exchange'e routelanir;
// exception yolunda cascade HIC yayinlanmaz (error-queue mesaji izsizdir — kabul, FR-008).
public class PriceAlarmEventHandlers
{
    public async Task<IntegrationEvents.NotificationSent> Handle(
        IntegrationEvents.PriceAlarmTriggered evt,
        MailAgent mailAgent,
        WebAppOptions webAppOptions,
        CancellationToken ct)
    {
        // E-posta bos → gonderim atlanir, iz "no-email" ile DUSER (R9).
        if (string.IsNullOrWhiteSpace(evt.Email))
            return new IntegrationEvents.NotificationSent(
                evt.UserId, evt.ProductId, evt.Email, Success: false, Detail: NotificationDetails.NoEmail);

        // Mutlak link: relatif yol mail istemcisinde yanlis host'a cozulur (Mailpit UI 404 bulgusu).
        var link = $"{webAppOptions.BaseUrl.TrimEnd('/')}/products/{evt.ProductId}";

        await mailAgent.SendPriceAlarmMailAsync(evt, link, ct);

        return new IntegrationEvents.NotificationSent(
            evt.UserId, evt.ProductId, evt.Email, Success: true, Detail: NotificationDetails.Sent);
    }
}