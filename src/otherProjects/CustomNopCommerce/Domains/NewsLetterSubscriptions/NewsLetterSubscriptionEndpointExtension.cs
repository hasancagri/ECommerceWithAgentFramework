using CustomNopCommerce.Domains.NewsLetterSubscriptions.Features.Commands;
using CustomNopCommerce.Domains.NewsLetterSubscriptions.Features.Queries;

namespace CustomNopCommerce.Domains.NewsLetterSubscriptions;

/// <summary>Bülten aboneliği feature endpoint'lerini tek grup altında toplar.</summary>
public static class NewsLetterSubscriptionEndpointExtension
{
    public static void AddNewsLetterSubscriptionGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/newsletter-subscriptions").WithTags("NewsLetterSubscriptions")
            .SubscribeGroupItemEndpoint()
            .UnsubscribeGroupItemEndpoint()
            .ListSubscriptionsGroupItemEndpoint();
    }
}
