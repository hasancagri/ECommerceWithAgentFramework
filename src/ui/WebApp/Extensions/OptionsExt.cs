#region

using Microsoft.Extensions.Options;
using WebApp.Options;

#endregion

namespace WebApp.Extensions;

public static class OptionsExt
{
    public static IServiceCollection AddOptionsExt(this IServiceCollection services)
    {
        services.AddOptions<GatewayOption>().BindConfiguration(nameof(GatewayOption)).ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<GatewayOption>(sp => sp.GetRequiredService<IOptions<GatewayOption>>().Value);

        // PostHog analytics (JS snippet layout'ta); key user-secrets'ten. ValidateOnStart YOK —
        // key olmasa da uygulama açılır, snippet basılmaz.
        services.AddOptions<PostHogOption>().BindConfiguration("PostHog");

        services.AddSingleton<PostHogOption>(sp => sp.GetRequiredService<IOptions<PostHogOption>>().Value);

        return services;
    }
}