using Microsoft.Extensions.Options;

namespace Procurement.Api.Options;

public static class OptionsExt
{
    public static IServiceCollection AddOptionsExt(this IServiceCollection services)
    {
        services.AddOptions<FeedPullOptions>().BindConfiguration(nameof(FeedPullOptions))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<FeedPullOptions>(sp => sp.GetRequiredService<IOptions<FeedPullOptions>>().Value);

        // 047: tedarikçi-başı feed ucu haritası (code → relatif path).
        services.AddOptions<SupplierFeedEndpointsOptions>().BindConfiguration(nameof(SupplierFeedEndpointsOptions))
            .ValidateOnStart();
        services.AddSingleton<SupplierFeedEndpointsOptions>(sp => sp.GetRequiredService<IOptions<SupplierFeedEndpointsOptions>>().Value);

        return services;
    }
}