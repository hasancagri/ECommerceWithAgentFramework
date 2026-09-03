namespace Identity.Server.Connect;

// 061 logout: dış agent (Claude Desktop) chat'ten "çıkış yap" dediğinde, o kullanıcının O client'a
// verdiği tüm authorization + token'ları iptal eder. Bearer korumalı (agent'ın kendi access token'ı);
// sub + client_id token'dan okunur — istemci başka client/kullanıcı adına logout edemez.
// Sonuç: refresh token ölür, yeni token alınamaz, consent kaydı silinir (sonraki bağlantı yeni consent).
// NOT: eldeki access token stateless JWT — doğal ömrü (mevcut default) dolana dek geçerli kalır.
public static class AgentLogoutEndpoint
{
    public static void MapAgentLogoutEndpoint(this WebApplication app) =>
        app.MapPost("/connect/agent-logout", HandleAsync).RequireAuthorization("agent-authenticated");

    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictAuthorizationManager authorizationManager,
        IOpenIddictTokenManager tokenManager)
    {
        var subject = context.User.FindFirst(Claims.Subject)?.Value;
        var clientId = context.User.FindFirst(Claims.ClientId)?.Value;

        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(clientId))
            return Results.BadRequest(new { error = "invalid_token", error_description = "sub/client_id yok." });

        var ct = context.RequestAborted;

        // TUZAK: Find*Async'in `client` parametresi ClientId string DEĞİL, application'ın primary key'i.
        // ClientId claim'inden application'ı bul, GetIdAsync ile PK'yı al — yoksa FindAsync boş döner,
        // iptal sessizce no-op olur (uç 200 döner ama token'lar yaşamaya devam eder).
        var application = await applicationManager.FindByClientIdAsync(clientId, ct);
        if (application is null)
            return Results.Ok(new { status = "logged_out" });

        var applicationId = (await applicationManager.GetIdAsync(application, ct))!;

        // Bağlı token'ları önce iptal et (authorization silinince yetim kalmasın), sonra authorization'ları.
        // status/type süzgeci yok (null) — bu kullanıcı+client'ın TÜM token/authorization'ları iptal.
        // TUZAK: FindAsync bir IAsyncEnumerable stream'i (DbDataReader açık). Stream iterate edilirken
        // AYNI DbContext'te TryRevokeAsync (SaveChanges) çağrılırsa reader meşgul → concurrency → Try
        // sessizce false döner, hiçbir şey iptal olmaz. Önce listeye TOPLA (reader kapansın), SONRA iptal.
        var tokens = new List<object>();
        await foreach (var token in tokenManager.FindAsync(subject, applicationId, status: null, type: null, ct))
            tokens.Add(token);

        var authorizations = new List<object>();
        await foreach (var authorization in authorizationManager.FindAsync(
            subject, applicationId, status: null, type: null, scopes: null, ct))
            authorizations.Add(authorization);

        foreach (var token in tokens)
            await tokenManager.TryRevokeAsync(token, ct);

        foreach (var authorization in authorizations)
            await authorizationManager.TryRevokeAsync(authorization, ct);

        return Results.Ok(new { status = "logged_out" });
    }
}