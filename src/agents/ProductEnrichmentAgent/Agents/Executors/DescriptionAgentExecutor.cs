namespace ProductEnrichmentAgent.Agents.Executors;

// Workflow'un ilk adimi: IncompleteProduct girer, aciklamayi yalniz eksikse uretip Catalog'a yazar
// (FR-005: dolu alani atla), sonucu EnrichmentState icinde Image adimina gonderir. Ham uretim
// DescriptionAgent'e devredilir; executor yaz-ya-da-atla kararini ve FieldResult eslemesini sahiplenir.
public sealed class DescriptionAgentExecutor(
    DescriptionAgent descriptionAgent,
    EnrichmentMcpClient mcp,
    ILogger<DescriptionAgentExecutor> logger)
    : Executor("DescriptionAgent", declareCrossRunShareable: true)
{
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
        protocolBuilder
            .SendsMessage<EnrichmentState>()
            .ConfigureRoutes(routes => routes.AddHandler<IncompleteProduct>(HandleAsync));

    private async ValueTask HandleAsync(
        IncompleteProduct product,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var result = new EnrichmentResult { ProductId = product.Id, Name = product.Name };

        result.Description = product.HasDescription
            ? FieldResult.Skipped
            : await EnrichAsync(product, cancellationToken);

        await context.SendMessageAsync(new EnrichmentState(product, result), null, cancellationToken);
    }

    private async Task<FieldResult> EnrichAsync(IncompleteProduct p, CancellationToken ct)
    {
        try
        {
            var description = await descriptionAgent.GenerateAsync(p.Name, p.Brand.ToString(), ct);
            if (string.IsNullOrWhiteSpace(description))
                return FieldResult.Failed;

            var ok = await mcp.SetDescriptionAsync(p.Id, description, ct);
            return ok ? FieldResult.Ok : FieldResult.Failed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Aciklama zenginlestirme basarisiz: {Id} ({Name})", p.Id, p.Name);
            return FieldResult.Failed;
        }
    }
}