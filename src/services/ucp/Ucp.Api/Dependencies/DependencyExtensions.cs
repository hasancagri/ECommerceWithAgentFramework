namespace Ucp.Api.Dependencies;

/// <summary>Scrutor otomatik DI: marker arayüzlerini (ITransient/IScoped/ISingletonDependency) tarayıp kaydeder.</summary>
public static class DependencyExtensions
{
    public static void AddAllDependencies(this IServiceCollection serviceCollection)
    {
        // AsSelfWithInterfaces: concrete tip + arayüzler aynı instance. UCP servisleri (ExternalOrderClient,
        // HttpMessageSigner/Verifier, UcpWebhookSender) concrete tiple enjekte edilir — yalnız arayüz kaydı
        // "Unable to resolve concrete type" verirdi.
        serviceCollection.Scan(scan => scan
            .FromApplicationDependencies()
            .AddClasses(classes => classes.AssignableTo<ITransientDependency>())
                .AsSelfWithInterfaces().WithTransientLifetime()
            .AddClasses(classes => classes.AssignableTo<IScopedDependency>())
                .AsSelfWithInterfaces().WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo<ISingletonDependency>())
                .AsSelfWithInterfaces().WithSingletonLifetime()
        );
    }
}