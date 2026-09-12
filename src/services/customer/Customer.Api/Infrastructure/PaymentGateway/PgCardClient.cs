using System.Net.Http.Headers;
using Customer.Api.Infrastructure.PaymentGateway.Options;
using Customer.Api.Infrastructure.Tokenization;

namespace Customer.Api.Infrastructure.PaymentGateway;

/// <summary>
/// 075: <see cref="IPgCardClient"/> REST implementasyonu. PG'nin kart uçlarını (add-session/complete/
/// list/delete) tüketir; sağlayıcı PG'nin İÇİNDE (FR-016) — mağaza yalnız bu sözleşmeyi görür. Auth =
/// mevcut merchant token deseni (<see cref="IMerchantTokenProvider"/>; merchant kimliği
/// <see cref="MerchantInformation"/>'dan). DB okur → Scoped. PAN/CVV bu client'a HİÇ uğramaz — yalnız
/// opak handle'lar + gösterilebilir izdüşüm. Ağ/timeout → fail-closed (list null, add Success=false).
/// </summary>
public sealed class PgCardClient(
    IQuerySession session,
    IMerchantTokenProvider tokenProvider,
    IHttpClientFactory httpClientFactory,
    PgCardOptions options,
    ILogger<PgCardClient> logger) : IPgCardClient, IScopedDependency
{
    public const string HttpClientName = "pg-card";

    // PG istek/yanıt gövdeleri (sözleşme: contracts/pg-card-contract.md; alan adları PG kontratıyla hizalı).
    private sealed record StartSessionRequest(Guid merchantId, string conversationId, string callbackUrl);
    private sealed record StartSessionReply(string? addUrl, string? conversationId);
    private sealed record CompleteReply(string? status, string? pgUserHandle);
    private sealed record ListReply(List<PgCardReply>? cards);
    private sealed record PgCardReply(string cardHandle, string brand, string last4, int expiryMonth, int expiryYear, string? alias);
    private sealed record DeleteRequest(string userHandle, string cardHandle);
    private sealed record DeleteReply(bool deleted);

    public async Task<StartAddSessionResult> StartAddSessionAsync(Guid conversationId, CancellationToken ct)
    {
        var (merchant, http) = await AuthenticatedClientAsync(ct);
        if (merchant is null || http is null)
            return new StartAddSessionResult(false, null);

        try
        {
            var url = Combine(options.CardSessionsPath);
            var body = new StartSessionRequest(merchant.MerchantId, conversationId.ToString(), options.CallbackUrl);
            using var resp = await http.PostAsJsonAsync(url, body, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("PG add-session başarısız: {Status}", resp.StatusCode);
                return new StartAddSessionResult(false, null);
            }

            var reply = await resp.Content.ReadFromJsonAsync<StartSessionReply>(cancellationToken: ct);
            return string.IsNullOrWhiteSpace(reply?.addUrl)
                ? new StartAddSessionResult(false, null)
                : new StartAddSessionResult(true, reply!.addUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "PG add-session erişilemedi (fail-closed).");
            return new StartAddSessionResult(false, null);
        }
    }

    public async Task<CompleteAddResult> CompleteAddAsync(Guid conversationId, CancellationToken ct)
    {
        var (_, http) = await AuthenticatedClientAsync(ct);
        if (http is null)
            return new CompleteAddResult(PgAddStatus.Failure, null);

        try
        {
            var url = $"{Combine(options.CardSessionsPath)}/{conversationId}";
            using var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("PG complete-add başarısız: {Status}", resp.StatusCode);
                return new CompleteAddResult(PgAddStatus.Failure, null);
            }

            var reply = await resp.Content.ReadFromJsonAsync<CompleteReply>(cancellationToken: ct);
            return reply?.status switch
            {
                "success" when !string.IsNullOrWhiteSpace(reply.pgUserHandle)
                    => new CompleteAddResult(PgAddStatus.Success, reply.pgUserHandle),
                "pending" => new CompleteAddResult(PgAddStatus.Pending, null),
                _ => new CompleteAddResult(PgAddStatus.Failure, null) // failure/cancelled/bilinmeyen → kayıt yok (FR-010)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "PG complete-add erişilemedi.");
            return new CompleteAddResult(PgAddStatus.Failure, null);
        }
    }

    public async Task<IReadOnlyList<PgCard>?> ListCardsAsync(string userHandle, CancellationToken ct)
    {
        var (_, http) = await AuthenticatedClientAsync(ct);
        if (http is null)
            return null;

        try
        {
            var url = $"{Combine(options.CardsPath)}?userHandle={Uri.EscapeDataString(userHandle)}";
            using var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("PG list-cards başarısız: {Status}", resp.StatusCode);
                return null; // bayat kopya gösterme — getirilemiyor
            }

            var reply = await resp.Content.ReadFromJsonAsync<ListReply>(cancellationToken: ct);
            return (reply?.cards ?? [])
                .Select(c => new PgCard(c.cardHandle, c.brand, c.last4, c.expiryMonth, c.expiryYear, c.alias))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "PG list-cards erişilemedi.");
            return null;
        }
    }

    public async Task<bool> DeleteCardAsync(string userHandle, string cardHandle, CancellationToken ct)
    {
        var (_, http) = await AuthenticatedClientAsync(ct);
        if (http is null)
            return false;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, Combine(options.CardsPath))
            {
                Content = JsonContent.Create(new DeleteRequest(userHandle, cardHandle))
            };
            using var resp = await http.SendAsync(request, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("PG delete-card başarısız: {Status}", resp.StatusCode);
                return false;
            }

            var reply = await resp.Content.ReadFromJsonAsync<DeleteReply>(cancellationToken: ct);
            return reply?.deleted ?? false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "PG delete-card erişilemedi.");
            return false;
        }
    }

    // Merchant kimliğini yükler + named client'a Bearer set eder. Merchant kaydı yoksa (onboarding
    // eksik) fail-closed → null döner (çağıran dostane hata verir).
    private async Task<(MerchantInformation? Merchant, HttpClient? Http)> AuthenticatedClientAsync(CancellationToken ct)
    {
        var merchant = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
        if (merchant is null)
        {
            logger.LogWarning("PG kart: MerchantInformation kaydı yok (fail-closed).");
            return (null, null);
        }

        try
        {
            var token = await tokenProvider.GetTokenAsync(merchant.MerchantId, merchant.MerchantKey, ct);
            var http = httpClientFactory.CreateClient(HttpClientName);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return (merchant, http);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PG kart: merchant token alınamadı (fail-closed).");
            return (merchant, null);
        }
    }

    private string Combine(string path) => $"{options.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
}
