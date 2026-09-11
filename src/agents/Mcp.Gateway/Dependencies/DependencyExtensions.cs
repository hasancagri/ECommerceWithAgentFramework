namespace Mcp.Gateway.Dependencies;

/// <summary>Scrutor DI — concrete + arayüz aynı instance (concrete enjeksiyon; 072 DI dersi).</summary>
public static class DependencyExtensions
{
    public static void AddAllDependencies(this IServiceCollection serviceCollection)
    {
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