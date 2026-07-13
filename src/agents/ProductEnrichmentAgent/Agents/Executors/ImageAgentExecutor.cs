namespace ProductEnrichmentAgent.Agents.Executors;

// Workflow'un son adimi: EnrichmentState girer, EnrichmentResult'i workflow ciktisi olarak yield eder.
// Gorseli yalniz eksikse uretir (FR-005/FR-009); once File'a yukler (ProductId-idempotent), sonra
// URL'i Catalog'a yazar. Ham uretim ImageAgent'e devredilir; executor uretim->upload->set dikisini
// ve FieldResult eslemesini sahiplenir. declareCrossRunShareable: durumsuz instance urun basi kosuda paylasilir.
public sealed class ImageAgentExecutor(
    ImageAgent imageAgent,
    EnrichmentMcpClient mcp,
    ILogger<ImageAgentExecutor> logger)
    : Executor("ImageAgent", declareCrossRunShareable: true)
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        protocolBuilder
            .YieldsOutput<EnrichmentResult>()
            .ConfigureRoutes(routes => routes.AddHandler<EnrichmentState>(HandleAsync));

    private async ValueTask HandleAsync(
        EnrichmentState message,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var (product, result) = message;

        result.Image = product.HasImage
            ? FieldResult.Skipped
            : await EnrichAsync(product, cancellationToken);

        await context.YieldOutputAsync(result, cancellationToken);
    }

    private async Task<FieldResult> EnrichAsync(IncompleteProduct p, CancellationToken ct)
    {
        try
        {
            var bytes = await imageAgent.GenerateAsync(p.Name, p.Brand.ToString(), ct);
            if (bytes is null || bytes.Length == 0)
                return FieldResult.Failed;

            // Uretim -> upload dikisi (spec edge): once File'a yukle (ProductId-idempotent), sonra URL'i yaz.
            var url = await mcp.UploadImageAsync(p.Id, bytes, ct);
            if (string.IsNullOrWhiteSpace(url))
                return FieldResult.Failed;

            var ok = await mcp.SetImageAsync(p.Id, url, ct);
            return ok ? FieldResult.Ok : FieldResult.Failed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gorsel zenginlestirme basarisiz: {Id} ({Name})", p.Id, p.Name);
            return FieldResult.Failed;
        }
    }
}