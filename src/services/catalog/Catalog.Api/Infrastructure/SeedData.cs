using Catalog.Api.Domains.Products;
using Shared.Enums;
using Shared.Payloads;
using Wolverine.Marten.Publishing;

namespace Catalog.Api.Infrastructure;

// IInitialData, Wolverine runtime baslamadan once (MartenActivator icinde) calistigi icin
// orada PublishAsync patlar. Bu yuzden seed'i IHostedService olarak kaydedip Wolverine'den
// SONRA baslatiyoruz (Program.cs'te UseWolverine'den sonra AddHostedService).
public class SeedData(IServiceProvider serviceProvider) : IHostedService
{
    private const int ProductCount = 1000;

    // Her urun, Stock.Api'de bu adetle stok kaydina baslar (baslangic degeri).
    private const int InitialStock = 100;

    // Her marka icin gercekci model serileri; isimler bunlardan turetilir.
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

    public async Task StartAsync(CancellationToken cancellation)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var sessionFactory = scope.ServiceProvider.GetRequiredService<OutboxedSessionFactory>();

        // Outbox'a enlist edilmis session: asagidaki PublishAsync dogrudan broker'a gitmez,
        // ayni Marten transaction'ina (outbox tablosuna) yazilir. Boylece urunler ve event TEK
        // commit'te kaydedilir, dual-write penceresi kapanir.
        await using var session = sessionFactory.OpenSession(bus);

        var alreadySeeded = await session.Query<Product>().AnyAsync(token: cancellation);
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
                description: $"{name} - brand new {brand} product.",
                price: price,
                sku: $"SKU-{i:D5}",
                brand: brand,
                imageUrl: null));
        }

        session.Store(products.ToArray());

        // Tek event, liste payload: Stock.Api 1000 ayri mesaj yerine tek mesajla toplu stok acar.
        var items = products
            .Select(product => new ProductStockInfo(product.Id, InitialStock))
            .ToList();
        await bus.PublishAsync(new IntegrationEvents.ProductCreatedEvent(items));

        // Urunler + outbox mesaji tek commit; commit sonrasi Wolverine event'i broker'a relay eder.
        await session.SaveChangesAsync(cancellation);
    }

    public Task StopAsync(CancellationToken cancellation) => Task.CompletedTask;
}