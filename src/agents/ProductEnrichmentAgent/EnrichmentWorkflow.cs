namespace ProductEnrichmentAgent;

// Urun basina Agent Framework Workflow'u: Description -> Image (sirali graf). Her executor kendi
// alanini BAGIMSIZ uretip yazar (FR-006); dolu alanlar atlanir (FR-005/FR-009); dayaniklilik/idempotency
// cagrilan servislerin MCP handler'larindadir. Graf bir kez kurulur, urun basina InProcessExecution ile
// kosulur; cikti WithOutputFrom(image) uzerinden WorkflowOutputEvent olarak (EnrichmentResult) alinir.
public sealed class EnrichmentWorkflow(
    DescriptionAgentExecutor descriptionExecutor,
    ImageAgentExecutor imageExecutor)
{
    private readonly Workflow _workflow =
        new WorkflowBuilder(descriptionExecutor)
            .AddEdge<EnrichmentState>(descriptionExecutor, imageExecutor, condition: null)
            .WithOutputFrom(imageExecutor)
            .Build();

    public async Task<EnrichmentResult> RunAsync(IncompleteProduct product, CancellationToken ct)
    {
        await using var run = await InProcessExecution.RunAsync(_workflow, product, cancellationToken: ct);

        var output = run.NewEvents
            .OfType<WorkflowOutputEvent>()
            .Select(e => e.As<EnrichmentResult>())
            .LastOrDefault(r => r is not null);

        // Cikti beklenmedik sekilde alinamazsa urunu bozmadan basarisiz-alanli sonuc dondur (FR-007 raporu).
        return output ?? new EnrichmentResult
        {
            ProductId = product.Id,
            Name = product.Name,
            Description = FieldResult.Failed,
            Image = FieldResult.Failed,
        };
    }
}