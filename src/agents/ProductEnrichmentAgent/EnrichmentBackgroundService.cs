namespace ProductEnrichmentAgent;

// Tetik (research D1): acilista eksik urunleri Catalog'dan MCP ile ceker ve TOPLU isler (US2).
// Her urun ayri try/catch; tek hata kosuyu durdurmaz (FR-007). Az-eszamanli (sirali) — OpenAI
// image oran sinirlari icinde kalir. Kosu sonunda urun basina basari/atlandi/hata raporu loglanir.
public sealed class EnrichmentBackgroundService(
    EnrichmentWorkflow workflow,
    EnrichmentMcpClient mcp,
    ILogger<EnrichmentBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Downstream servisler (gateway, catalog, file, identity) hazir olsun diye kisa gecikme.
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
        catch (OperationCanceledException) { return; }

        try
        {
            var products = await mcp.ListIncompleteAsync(limit: 100, stoppingToken);
            logger.LogInformation("Enrichment: {Count} eksik urun bulundu.", products.Count);

            var results = new List<EnrichmentResult>();
            foreach (var product in products)
            {
                if (stoppingToken.IsCancellationRequested) break;
                try
                {
                    results.Add(await workflow.RunAsync(product, stoppingToken));
                }
                catch (Exception ex)
                {
                    // FR-007: tek urun hatasi toplu kosuyu durdurmaz.
                    logger.LogError(ex, "Urun zenginlestirilemedi: {Id} ({Name})", product.Id, product.Name);
                }
            }

            LogReport(results);
        }
        catch (OperationCanceledException)
        {
            // kapaniyor
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Enrichment kosusu basarisiz.");
        }
    }

    private void LogReport(IReadOnlyList<EnrichmentResult> results)
    {
        // SC-004/SC-002 gozlemi: urun basina durum + ozet sayaclar.
        foreach (var r in results)
            logger.LogInformation("Enrichment sonucu {Name} ({Id}): aciklama={Desc}, gorsel={Image}",
                r.Name, r.ProductId, r.Description, r.Image);

        var completed = results.Count(r =>
            r.Description is not FieldResult.Failed && r.Image is not FieldResult.Failed);
        var failed = results.Count - completed;

        logger.LogInformation("Enrichment ozeti: {Total} urun islendi, {Completed} tam/atlandi, {Failed} basarisiz.",
            results.Count, completed, failed);
    }
}