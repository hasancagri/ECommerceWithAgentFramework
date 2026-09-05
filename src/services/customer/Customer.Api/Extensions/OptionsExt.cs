using Microsoft.Extensions.Options;

namespace Customer.Api.Extensions;

public static class OptionsExt
{
    public static IServiceCollection AddOptionsExt(this IServiceCollection services)
    {
        // DropShop vault (Identity + Payment.Api) config — section "DropShopVault".
        services.AddOptions<DropShopVaultOption>().BindConfiguration("DropShopVault")
            .ValidateDataAnnotations().ValidateOnStart();

        services.AddSingleton<DropShopVaultOption>(sp =>
            sp.GetRequiredService<IOptions<DropShopVaultOption>>().Value);

        return services;
    }
}
