namespace Procurement.Api.Seeding;

// Supplier kayıtları seed'le doğar (R2: statik kimlik — tie-break deterministik olur).
// İdempotent: Code ile bulunan kayıt güncellenir (Priority sabit, eşleme tablosu tazelenir).
public sealed class ProcurementSeedHostedService(
    IDocumentStore store,
    ILogger<ProcurementSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession();

        await SeedSupplierAsync(session, "supplier-a", "Tedarikçi A", priority: 1,
            CanonicalTaxonomy.SupplierAMappings, cancellationToken);
        await SeedSupplierAsync(session, "supplier-b", "Tedarikçi B", priority: 2,
            CanonicalTaxonomy.SupplierBMappings, cancellationToken);

        await session.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Procurement seed tamam: supplier-a (P1) + supplier-b (P2) + kategori eşlemeleri");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedSupplierAsync(
        IDocumentSession session, string code, string name, int priority,
        IReadOnlyList<CategoryMapping> mappings, CancellationToken ct)
    {
        var supplier = await session.Query<Supplier>()
            .FirstOrDefaultAsync(s => s.Code == code, ct);

        if (supplier is null)
        {
            var created = Supplier.Create(code, name, priority);
            if (!created.IsSuccess)
                throw new InvalidOperationException($"Supplier seed başarısız: {code}");
            supplier = created.Data!;
        }

        supplier.SetCategoryMappings(mappings);
        session.Store(supplier);
    }
}