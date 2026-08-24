namespace Personalization.Api.Domains.BehaviorSignals.Features.Commands;

// 048 US2: WebApp (BFF) batch gezinme sinyali gonderir. Kayip-toleransli: gecersiz oge atlanir
// (FR-013), gecerliler yazilir; toplu istek 202 doner. PII yok (opak kimlik + davranis alanlari).
public static class IngestBehaviorSignals
{
    // Govde ogesi (contracts/behavior-signal-line.md). WebApp BehaviorEvent record'unu yansitir.
    public record BehaviorSignalItemDto(
        string EventType,
        string? Channel,
        Guid? UserId,
        Guid AnonymousId,
        Guid SessionId,
        Guid? ProductId,
        string? Brand,
        string? Category,
        decimal? Price,
        string? SearchTerm,
        IReadOnlyList<Guid>? ShownProductIds,
        DateTime Timestamp,
        int SchemaVersion);

    public record IngestBehaviorSignalsCommand(IReadOnlyList<BehaviorSignalItemDto> Signals);

    [Transactional]
    public class IngestBehaviorSignalsCommandHandler(ILogger<IngestBehaviorSignalsCommandHandler> logger)
    {
        public async Task<FeatureResultModel> Handle(
            IngestBehaviorSignalsCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            if (cmd.Signals is null || cmd.Signals.Count == 0)
                return FeatureResultModel.Ok(); // bos batch = no-op

            var stored = 0;
            foreach (var dto in cmd.Signals)
            {
                var signal = BehaviorSignal.Create(
                    dto.EventType, dto.Channel, dto.UserId, dto.AnonymousId, dto.SessionId,
                    dto.ProductId, dto.Brand, dto.Category, dto.Price, dto.SearchTerm,
                    dto.ShownProductIds, dto.Timestamp, dto.SchemaVersion);

                if (!signal.IsSuccess)
                {
                    // FR-013: gecersiz oge atlanir, digerleri etkilenmez.
                    logger.LogWarning("Gecersiz gezinme sinyali atlandi (eventType={EventType}).", dto.EventType);
                    continue;
                }

                session.Store(signal.Data!);
                stored++;
            }

            if (stored > 0)
                await session.SaveChangesAsync(ct);

            return FeatureResultModel.Ok();
        }
    }
}