using IngestionAgent.Workflows._01_StagingGate;
using IngestionAgent.Workflows._02_DomainWrite;
using IngestionAgent.Workflows._02_DomainWrite.Agents;

namespace IngestionAgent.Workflows;

public static class HttpClients
{
    public const string Feeds = "feeds";
    public const string McpNoToken = "mcp-no-token";
}

// Run yaşam döngüsü: tek-run kilidi (FR-024, süreç içi SemaphoreSlim), IngestionRun dokümanı,
// feed çekimi (workflow DIŞI ön adım) ve kayıt başına workflow koşusu: gate → domain-write.
// Batch'i gören tek yer burası: mükerrer eleme + sayaçlar + run kapanışı serviste.
public sealed class IngestionRunService(
    IDocumentStore store,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    CatalogWriterAgent catalogAgent,
    StockWriterAgent stockAgent,
    DiscountWriterAgent discountAgent)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    // 202 dönmek için run'ı arka planda koşturur; kilit alınamazsa 409'luk false döner.
    public async Task<(bool Started, Guid RunId)> TryStartAsync(CancellationToken ct)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, ct))
            return (false, Guid.Empty);

        var run = IngestionRun.Start();

        try
        {
            await using var session = store.LightweightSession();
            session.Store(run);
            await session.SaveChangesAsync(ct);
        }
        catch
        {
            _gate.Release();
            throw;
        }

        _ = Task.Run(() => ExecuteAsync(run.Id), CancellationToken.None);
        return (true, run.Id);
    }

    private async Task ExecuteAsync(Guid runId)
    {
        try
        {
            // Ön adım (workflow dışı): feed'i çek. Erişilemezse run boş sayaçlarla kapanır.
            var supplierBase = configuration["services:supplier-api:http:0"] ?? "http://localhost:5299";
            var feedClient = new FeedClient(httpClientFactory, $"{supplierBase}/v1/feeds");
            var (fetchStatus, records) = await feedClient.FetchAsync(CancellationToken.None);

            var result = new SupplierRunResult
            {
                SupplierCode = FeedClient.SupplierCode,
                FetchStatus = fetchStatus
            };

            var workflow = BuildWorkflow();
            var seenExternalIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var record in records)
            {
                // Mükerrer harici kimlik: ilki esas, kalanlar Failed (batch'i gören tek yer).
                if (!string.IsNullOrWhiteSpace(record.ExternalId)
                    && !seenExternalIds.Add(record.ExternalId))
                {
                    result.Failed++;
                    continue;
                }

                // Kayıt başına koşu: executor'lar job'ı işaretler, sonuç buradan okunur.
                // 007 geçiş notu: eski tip tam adla — Workflows.RecordJob (yeni) ile çakışmasın.
                var job = new _01_StagingGate.RecordJob { Record = record };
                await using var run = await InProcessExecution.RunAsync(
                    workflow, job, cancellationToken: CancellationToken.None);

                if (job.Skipped) result.Skipped++;
                else if (job.Failure is not null) result.Failed++;
                else if (job.CatalogAction == CatalogWriterAgent.Created) result.New++;
                else result.Updated++;
            }

            await CloseRunAsync(runId, result);
        }
        catch (Exception ex)
        {
            // Beklenmeyen çökme: run Failed işaretlenir (kayıt hataları buraya düşmez, FR-020).
            await MarkRunFailedAsync(runId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private Workflow BuildWorkflow()
    {
        var gate = new StagingGateExecutor(store);
        var domainWrite = new DomainWriteExecutor(store, catalogAgent, stockAgent, discountAgent);

        var builder = new WorkflowBuilder(gate)
            .AddEdge(gate, domainWrite)
            .WithOutputFrom(domainWrite);
        return builder.Build();
    }

    private async Task CloseRunAsync(Guid runId, SupplierRunResult result)
    {
        await using var session = store.LightweightSession();
        var run = await session.LoadAsync<IngestionRun>(runId);
        if (run is null)
            return;

        run.Complete(result);
        session.Store(run);
        await session.SaveChangesAsync();
    }

    private async Task MarkRunFailedAsync(Guid runId)
    {
        try
        {
            await using var session = store.LightweightSession();
            var run = await session.LoadAsync<IngestionRun>(runId);
            if (run is null)
                return;

            run.Fail();
            session.Store(run);
            await session.SaveChangesAsync();
        }
        catch (Exception ex)
        {
        }
    }
}