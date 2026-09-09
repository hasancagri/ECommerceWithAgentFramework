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

        // 070: DropShop onboarding (PG Merchant.Api MCP + Identity) — section "DropShopOnboarding".
        // Alanlar opsiyonel: config yoksa tool'lar dostane "yapılamıyor" döner (IsConfigured).
        services.AddOptions<Customer.Api.Onboarding.DropShopOnboardingOption>()
            .BindConfiguration("DropShopOnboarding")
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<Customer.Api.Onboarding.DropShopOnboardingOption>(sp =>
            sp.GetRequiredService<IOptions<Customer.Api.Onboarding.DropShopOnboardingOption>>().Value);

        return services;
    }
}
