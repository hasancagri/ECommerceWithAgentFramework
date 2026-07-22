namespace IngestionAgent.Api;

// Ingestion uçları (contracts/ingestion-api.md). Tümü anonim (kullanıcı kararı: token yalnız
// alışveriş akışında); ileride yetki gerekirse scope-tabanlı tek satırla eklenir.
public static class IngestionEndpoints
{
    public static void MapIngestionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/ingestion").WithTags("Ingestion");

        // FR-007: aktarımı tetikle. 202 + runId; run sürüyorsa 409 (FR-024).
        group.MapPost("/runs", async (IngestionRunService service, CancellationToken ct) =>
        {
            var (started, runId) = await service.TryStartAsync(ct);
            return started
                ? Results.Accepted($"/v1/ingestion/runs/{runId}", new { runId })
                : Results.Conflict(new { error = "RUN_ALREADY_IN_PROGRESS" });
        }).WithName("StartIngestionRun");

        // Run listesi: yeniden eskiye, toplam sayaç özetiyle.
        group.MapGet("/runs", async (IQuerySession session, CancellationToken ct) =>
        {
            var runs = await session.Query<IngestionRun>()
                .OrderByDescending(r => r.StartedAtUtc)
                .Take(20)
                .ToListAsync(ct);

            return Results.Json(runs.Select(RunDto));
        }).WithName("GetIngestionRuns");

        // FR-022: run özeti, tedarikçi kırılımlı.
        group.MapGet("/runs/{id:guid}", async (Guid id, IQuerySession session, CancellationToken ct) =>
        {
            var run = await session.LoadAsync<IngestionRun>(id, ct);
            return run is null ? Results.NotFound() : Results.Json(RunDto(run));
        }).WithName("GetIngestionRun");

    }

    private static object RunDto(IngestionRun run) => new
    {
        run.Id,
        Status = run.Status.ToString(),
        run.StartedAtUtc,
        run.FinishedAtUtc,
        Suppliers = run.Suppliers.Select(s => new
        {
            s.SupplierCode,
            FetchStatus = s.FetchStatus.ToString(),
            s.New,
            s.Updated,
            s.Skipped,
            s.Failed
        })
    };
}