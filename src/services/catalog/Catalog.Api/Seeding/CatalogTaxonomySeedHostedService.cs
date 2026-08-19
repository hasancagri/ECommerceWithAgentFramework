namespace Catalog.Api.Seeding;

// 041: kanonik Category>SubCategory ağacının Catalog kopyası (R3 — iki BC'de AYRI seed; sözleşme AD'dır).
// Procurement kendi kopyasını CanonicalTaxonomy ile taşır; adlar birebir aynı olmak zorunda.
// İdempotent get-or-create: NormalizedName teklik anahtarıdır (016 düzeni).
public sealed class CatalogTaxonomySeedHostedService(
    IDocumentStore store,
    ILogger<CatalogTaxonomySeedHostedService> logger) : IHostedService
{
    private static readonly (string Top, string[] Subs)[] Tree =
    [
        ("Elektronik", ["Telefon", "Kulaklık", "Televizyon"]),
        ("Ev Aletleri", ["Kahve Makinesi", "Süpürge", "Blender"]),
        ("Moda", ["Çanta", "Cüzdan"]),
        ("Kişisel Bakım", ["Tıraş Makinesi", "Saç Kurutma"]),
        ("Spor", ["Yoga Matı", "Dambıl"]),
        ("Ofis", ["Klavye", "Mouse"]),
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession();

        foreach (var (top, subs) in Tree)
        {
            var parent = await GetOrCreateAsync(session, top, parentId: null, cancellationToken);
            foreach (var sub in subs)
                await GetOrCreateAsync(session, sub, parent.Id, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Kanonik taksonomi seed tamam: {Tops} üst kategori", Tree.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task<Category> GetOrCreateAsync(
        IDocumentSession session, string name, Guid? parentId, CancellationToken ct)
    {
        var normalized = NameNormalization.Normalize(name);
        var existing = await session.Query<Category>()
            .FirstOrDefaultAsync(c => c.NormalizedName == normalized, ct);
        if (existing is not null)
            return existing;

        var created = Category.Create(name, parentCategoryId: parentId);
        if (!created.IsSuccess)
            throw new InvalidOperationException($"Taksonomi seed başarısız: {name}");

        var category = created.Data!;
        category.SetPublished(true);
        session.Store(category);
        return category;
    }
}