namespace Ucp.Api.Domains.Sessions.Features.Commands;

/// <summary>
/// Session'ı iptal eder (POST /ucp/checkout_sessions/{id}/cancel) — terminal-olmayan durumdan canceled.
/// Tamamlanmış/süreçteki session iptal edilmez (aggregate reddeder → 409/BadRequest).
/// </summary>
public static class CancelSession
{
    public record CancelSessionCommand(string SessionId, string? Reason);

    [Transactional]
    public class CancelSessionHandler
    {
        public async Task<FeatureObjectResultModel<SessionResult>> Handle(
            CancelSessionCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var agg = await SessionStore.LoadAsync(session, cmd.SessionId, ct);
            if (agg is null) return FeatureObjectResultModel<SessionResult>.NotFound();

            var canceled = agg.Cancel(cmd.Reason);
            if (!canceled.IsSuccess) return FeatureObjectResultModel<SessionResult>.Error(canceled.Messages);

            session.Update(agg);
            return FeatureObjectResultModel<SessionResult>.Ok(SessionResult.From(agg));
        }
    }
}