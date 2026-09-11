namespace Ucp.Api.Domains.Sessions.Features.Queries;

/// <summary>Session mevcut durumunu döner (GET /ucp/checkout_sessions/{id}); yalnız okur.</summary>
public static class GetSession
{
    public record GetSessionQuery(string SessionId);

    public class GetSessionHandler
    {
        public async Task<FeatureObjectResultModel<SessionResult>> Handle(
            GetSessionQuery query, IQuerySession session, CancellationToken ct)
        {
            var agg = await SessionStore.LoadAsync(session, query.SessionId, ct);
            if (agg is null) return FeatureObjectResultModel<SessionResult>.NotFound();

            return FeatureObjectResultModel<SessionResult>.Ok(SessionResult.From(agg));
        }
    }
}