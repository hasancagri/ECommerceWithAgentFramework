namespace Ucp.Api.Domains.Sessions.Features.Commands;

/// <summary>
/// Session'a kargo seçeneği seçer (fulfillment uzantısı) — bedel totals'a yansır; sonra ready değerlendirilir.
/// UpdateSession bunu birleşik yapar; bu slice tekil kargo çağrısı içindir (FR-017).
/// </summary>
public static class SelectFulfillment
{
    public record SelectFulfillmentCommand(string SessionId, string SelectedOptionId, string? DestinationId);

    [Transactional]
    public class SelectFulfillmentHandler
    {
        public async Task<FeatureObjectResultModel<SessionResult>> Handle(
            SelectFulfillmentCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var agg = await SessionStore.LoadAsync(session, cmd.SessionId, ct);
            if (agg is null) return FeatureObjectResultModel<SessionResult>.NotFound();

            var ful = FulfillmentResolver.Resolve(cmd.SelectedOptionId, cmd.DestinationId ?? "", agg.Totals.SubtotalMinor);
            if (!ful.IsSuccess) return FeatureObjectResultModel<SessionResult>.Error(ful.Messages);

            var selected = agg.SelectFulfillment(ful.Data!);
            if (!selected.IsSuccess) return FeatureObjectResultModel<SessionResult>.Error(selected.Messages);

            agg.MarkReadyIfComplete(DateTimeOffset.UtcNow);
            session.Update(agg);
            return FeatureObjectResultModel<SessionResult>.Ok(SessionResult.From(agg));
        }
    }
}