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

        // 042: davranış log yazıcısı ayarları — Directory'yi AppHost enjekte eder (BehaviorLog__Directory).
        services.AddOptions<BehaviorLogOptions>().BindConfiguration("BehaviorLog");

        services.AddSingleton<BehaviorLogOptions>(sp => sp.GetRequiredService<IOptions<BehaviorLogOptions>>().Value);

        return services;
    }
}