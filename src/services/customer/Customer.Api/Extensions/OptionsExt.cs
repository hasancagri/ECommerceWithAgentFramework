using Microsoft.Extensions.Options;

namespace Customer.Api.Extensions;

public static class OptionsExt
{
    public static IServiceCollection AddOptionsExt(this IServiceCollection services)
    {
        // 076: DropShopVault (kart-saklama) config söküldü. Yalnız onboarding kaldı.
        // 070: DropShop onboarding (PG Merchant.Api MCP + Identity) — section "DropShopOnboarding".
        // Alanlar opsiyonel: config yoksa tool'lar dostane "yapılamıyor" döner (IsConfigured).
        services.AddOptions<Customer.Api.Onboarding.DropShopOnboardingOption>()
            .BindConfiguration("DropShopOnboarding")
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<Customer.Api.Onboarding.DropShopOnboardingOption>(sp =>
            sp.GetRequiredService<IOptions<Customer.Api.Onboarding.DropShopOnboardingOption>>().Value);

        // 078: hosted credential-giriş ekranı (link tabanı + ömür) — section "CredentialEntryOptions".
        services.AddOptions<Customer.Api.Options.CredentialEntryOptions>()
            .BindConfiguration(nameof(Customer.Api.Options.CredentialEntryOptions))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<Customer.Api.Options.CredentialEntryOptions>(sp =>
            sp.GetRequiredService<IOptions<Customer.Api.Options.CredentialEntryOptions>>().Value);

        return services;
    }
}
