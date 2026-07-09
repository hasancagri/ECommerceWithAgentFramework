namespace Catalog.Api.Infrastructure;

public class SeedData(IServiceProvider serviceProvider)
    : IHostedService
{
    private const int ProductCount = 200;
    private const int MinStock = 1;
    private const int MaxStock = 100;

    private static readonly Dictionary<BrandType, string[]> ModelsByBrand = new()
    {
        [BrandType.Apple] = ["iPhone", "iPad", "MacBook Pro", "AirPods", "Apple Watch"],
        [BrandType.Samsung] = ["Galaxy S", "Galaxy Tab", "Galaxy Watch", "Galaxy Buds", "QLED TV"],
        [BrandType.Sony] = ["WH-1000XM", "PlayStation", "Bravia TV", "Alpha Camera", "WF Earbuds"],
        [BrandType.Nike] = ["Air Max", "Air Force 1", "Pegasus", "Dunk Low", "Zoom Fly"],
        [BrandType.Adidas] = ["Ultraboost", "Stan Smith", "Superstar", "Gazelle", "Samba"],
        [BrandType.Lenovo] = ["ThinkPad", "IdeaPad", "Legion", "Yoga", "ThinkCentre"],
        [BrandType.Dell] = ["XPS", "Inspiron", "Latitude", "Alienware", "OptiPlex"],
        [BrandType.Hp] = ["Pavilion", "Spectre", "EliteBook", "Omen", "ProBook"],
        [BrandType.Asus] = ["ZenBook", "ROG Strix", "TUF Gaming", "VivoBook", "ProArt"],
        [BrandType.Xiaomi] = ["Redmi Note", "Mi", "Poco", "Redmi", "Mi Pad"]
    };

    //Burada Dual-Write mantığı korunur
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var sessionFactory = scope.ServiceProvider.GetRequiredService<OutboxedSessionFactory>();

        await using var session = sessionFactory.OpenSession(bus);

        var alreadySeeded = await session.Query<Product>()
            .AnyAsync(token: cancellationToken);
        if (alreadySeeded)
            return;

        var brands = Enum.GetValues<BrandType>();

        var products = new List<Product>(ProductCount);
        for (var i = 1; i <= ProductCount; i++)
        {
            var brand = brands[i % brands.Length];
            var models = ModelsByBrand[brand];
            var model = models[i / brands.Length % models.Length];
            var edition = i / (brands.Length * models.Length) + 1;

            var name = $"{brand} {model} {edition}";
            var price = 10m * (i % 100 + 1);

            products.Add(Product.Create(
                name: name,
                description: "",
                price: price,
                sku: $"SKU-{i:D5}",
                brand: brand,
                imageUrl: null));
        }

        session.Store(products.ToArray());

        var items = products
            .Select(product => new ProductStockInfo(product.Id, Random.Shared.Next(MinStock, MaxStock + 1)))
            .ToList();
        await bus.PublishAsync(new IntegrationEvents.ProductCreatedEvent(items));
        await session.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}