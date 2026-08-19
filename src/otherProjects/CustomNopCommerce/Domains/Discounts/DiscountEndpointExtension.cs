using CustomNopCommerce.Domains.Discounts.Features.Commands;
using CustomNopCommerce.Domains.Discounts.Features.Queries;

namespace CustomNopCommerce.Domains.Discounts;

/// <summary>İndirim feature endpoint'lerini tek grup altında toplar.</summary>
public static class DiscountEndpointExtension
{
    public static void AddDiscountGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/discounts").WithTags("Discounts")
            .CreateDiscountGroupItemEndpoint()
            .RecordDiscountUsageGroupItemEndpoint()
            .ValidateCouponGroupItemEndpoint()
            .ListDiscountsGroupItemEndpoint();
    }
}
