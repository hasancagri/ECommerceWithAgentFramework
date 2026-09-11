namespace Ucp.Api.Domains.Sessions.Features;

/// <summary>UCP-facing SessionId ile aggregate yükleme (Marten identity Guid; SessionId indeksli).</summary>
public static class SessionStore
{
    public static Task<UcpCheckoutSession?> LoadAsync(IQuerySession session, string sessionId, CancellationToken ct)
        => session.Query<UcpCheckoutSession>().Where(x => x.SessionId == sessionId).FirstOrDefaultAsync(ct)!;
}