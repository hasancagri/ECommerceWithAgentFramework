using CustomNopCommerce.Domains.QueuedEmails.Features.Commands;
using CustomNopCommerce.Domains.QueuedEmails.Features.Queries;

namespace CustomNopCommerce.Domains.QueuedEmails;

/// <summary>Kuyruğa alınmış e-posta feature endpoint'lerini tek grup altında toplar.</summary>
public static class QueuedEmailEndpointExtension
{
    public static void AddQueuedEmailGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/queued-emails").WithTags("QueuedEmails")
            .QueueEmailGroupItemEndpoint()
            .MarkEmailSentGroupItemEndpoint()
            .ListPendingEmailsGroupItemEndpoint();
    }
}
