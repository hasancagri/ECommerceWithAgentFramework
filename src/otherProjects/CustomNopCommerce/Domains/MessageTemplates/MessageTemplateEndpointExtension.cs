using CustomNopCommerce.Domains.MessageTemplates.Features.Commands;
using CustomNopCommerce.Domains.MessageTemplates.Features.Queries;

namespace CustomNopCommerce.Domains.MessageTemplates;

/// <summary>Mesaj şablonu feature endpoint'lerini tek grup altında toplar.</summary>
public static class MessageTemplateEndpointExtension
{
    public static void AddMessageTemplateGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/message-templates").WithTags("MessageTemplates")
            .CreateMessageTemplateGroupItemEndpoint()
            .UpdateMessageTemplateBodyGroupItemEndpoint()
            .ListMessageTemplatesGroupItemEndpoint();
    }
}
