
namespace Order.Api.Contracts.Refit;

public static class RefitConfiguration
{
    public static IServiceCollection AddRefitConfigurationExtension(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<AuthenticatedHttpClientHandler>();
        services.AddScoped<ClientAuthenticatedHttpClientHandler>();

        services.AddOptions<IdentityOption>()
            .BindConfiguration(nameof(IdentityOption))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        services.AddSingleton<IdentityOption>(sp => sp.GetRequiredService<IOptions<IdentityOption>>().Value);

        services.AddOptions<ClientSecretOption>().BindConfiguration(nameof(ClientSecretOption))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<ClientSecretOption>(sp =>
            sp.GetRequiredService<IOptions<ClientSecretOption>>().Value);

        services.AddRefitClient<IPaymentService>().ConfigureHttpClient(configure =>
            {
                var addressUrlOption = configuration.GetSection(nameof(AddressUrlOption)).Get<AddressUrlOption>();
                configure.BaseAddress = new Uri(addressUrlOption!.PaymentUrl);
            })
            .AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
            .AddHttpMessageHandler<ClientAuthenticatedHttpClientHandler>();

        return services;
    }
}