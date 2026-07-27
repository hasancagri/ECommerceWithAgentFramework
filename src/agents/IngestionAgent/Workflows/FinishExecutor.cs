namespace IngestionAgent.Workflows;

// Terminal collector (015): HER yol (başarı veya short-circuit) buradan geçer. Taban tiple dinler
// (üç yazıcının sonucu da WriterResult'tan türer), sonucu olduğu gibi döndürür; WithOutputFrom
// bunu workflow çıktısına çevirir. Handler çıktı YOKSA run'ı başarı saymaz (S4, FR-005).
public sealed class FinishExecutor() : Executor<WriterResult, WriterResult>("finish")
{
    public override ValueTask<WriterResult> HandleAsync(
        WriterResult outcome, IWorkflowContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(outcome);
}