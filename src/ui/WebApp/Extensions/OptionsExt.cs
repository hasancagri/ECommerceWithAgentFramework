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

        // DropShop gateway (Merchant.Api /mcp + Identity) config — section "DropShopGateway".
        services.AddOptions<DropShopGatewayOption>().BindConfiguration("DropShopGateway").ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<DropShopGatewayOption>(sp =>
            sp.GetRequiredService<IOptions<DropShopGatewayOption>>().Value);
        return services;
    }
}