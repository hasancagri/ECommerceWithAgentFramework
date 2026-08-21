using Catalog.Api.Domains.SpecificationAttributes;

namespace Catalog.Api.Seeding;

// 043: kanonik özellik registry'sinin Catalog kopyası (Procurement CanonicalSpecs ile AD-hizalı;
// iki BC'de AYRI seed — taksonomi deseni). İdempotent get-or-create: NormalizedName teklik anahtarı;
// mevcut attribute'a eksik option'lar da idempotent eklenir (AddOption mükerrer guard'ı).
public sealed class CatalogSpecSeedHostedService(
    IDocumentStore store,
    ILogger<CatalogSpecSeedHostedService> logger) : IHostedService
{
    private static readonly (string Name, int Order, string[] Options)[] Registry =
    [
        ("Renk", 1, ["Siyah", "Beyaz", "Gri", "Kırmızı"]),
        ("Materyal", 2, ["Çelik", "Plastik", "Cam", "Ahşap"]),
        ("Garanti Süresi", 3, ["1 Yıl", "2 Yıl", "3 Yıl"]),
        ("Enerji Sınıfı", 4, ["A", "B", "C"]),
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession();

        foreach (var (name, order, options) in Registry)
        {
            var normalized = NameNormalization.Normalize(name);
            var attribute = await session.Query<SpecificationAttribute>()
                .FirstOrDefaultAsync(a => a.NormalizedName == normalized, cancellationToken);

            if (attribute is null)
            {
                var created = SpecificationAttribute.Create(name, filterable: true, displayOrder: order);
                if (!created.IsSuccess)
                    throw new InvalidOperationException($"Spec seed başarısız: {name}");
                attribute = created.Data!;
            }

            for (var i = 0; i < options.Length; i++)
                attribute.AddOption(options[i], displayOrder: i + 1); // mükerrer = sessiz Error, idempotent

            session.Store(attribute);
        }

        await session.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Spec registry seed tamam: {Count} attribute", Registry.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
