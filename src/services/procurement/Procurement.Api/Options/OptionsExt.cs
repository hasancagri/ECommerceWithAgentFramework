using Microsoft.Extensions.Options;

namespace Procurement.Api.Options;

public static class OptionsExt
{
    public static IServiceCollection AddOptionsExt(this IServiceCollection services)
    {
        services.AddOptions<FeedPullOptions>().BindConfiguration(nameof(FeedPullOptions))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<FeedPullOptions>(sp => sp.GetRequiredService<IOptions<FeedPullOptions>>().Value);

        services.AddOptions<EnrichmentOptions>().BindConfiguration(EnrichmentOptions.SectionName)
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<EnrichmentOptions>(sp => sp.GetRequiredService<IOptions<EnrichmentOptions>>().Value);

        return services;
    }
}