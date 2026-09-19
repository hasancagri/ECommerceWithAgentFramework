using Customer.Api.Domains.MerchantInformations.Features.Commands;

namespace Customer.Api;

// 078 D1/US3: hosted credential-giriş ekranı — tek sayfalık gömülü HTML (Razor/SPA yok). ANONİM:
// token = yetki (İlke V v1.11.1 capability-link istisnası; scope zorlaması link üretiminde).
// Bilinmeyen/tüketilmiş/süresi geçmiş token her iki uçta da NÖTR 404 (token doğruluğu sızdırılmaz).
// Ekran yazma-only: mevcut MerchantId/Key ASLA gösterilmez (FR-005).
public static class CredentialEntryEndpointExtension
{
    public static void MapCredentialEntryEndpoints(this WebApplication app)
    {
        // GET: form. Oturumu TÜKETMEZ (form açıp vazgeçmek linki öldürmez, süre öldürür — D2).
        app.MapGet("/merchant-credentials/{token}", async (
            string token, IQuerySession session, CancellationToken ct) =>
        {
            var entry = await session.Query<Domains.MerchantInformations.CredentialEntrySession>()
                .FirstOrDefaultAsync(x => x.Token == token, ct);

            return entry is not null && entry.IsUsable(DateTimeOffset.UtcNow)
                ? Html(FormPage(token, error: null), StatusCodes.Status200OK)
                : Html(NotFoundPage, StatusCodes.Status404NotFound);
        });

        // POST: doğrula + kaydet. Başarı token'ı tüketir; geçersiz ikili oturumu YAŞATIR (düzeltilebilir).
        app.MapPost("/merchant-credentials/{token}", async (
            string token, HttpRequest request, IMessageBus bus, CancellationToken ct) =>
        {
            var form = await request.ReadFormAsync(ct);
            var result = await bus.InvokeAsync<FeatureObjectResultModel<SubmitMerchantCredentials.SubmitMerchantCredentialsResponse>>(
                new SubmitMerchantCredentials.SubmitMerchantCredentialsCommand(
                    token,
                    form["merchantId"].ToString(),
                    form["merchantKey"].ToString()), ct);

            if (result.IsSuccess)
                return Html(SuccessPage(result.Data!.Verified), StatusCodes.Status200OK);

            var messages = result.Messages ?? [];
            if (messages.Any(m => m.Code == CustomerResourceConstants.RECORD_NOT_FOUND))
                return Html(NotFoundPage, StatusCodes.Status404NotFound);

            var error = messages.Any(m => m.Code == CustomerResourceConstants.MERCHANT_CREDENTIALS_INVALID)
                ? "Girilen MerchantId + MerchantKey ikilisi ödeme sağlayıcısında doğrulanamadı. Değerleri kontrol edip yeniden deneyin."
                : "Alanları kontrol edin: MerchantId geçerli bir GUID, MerchantKey boş olmayan bir değer olmalı.";
            return Html(FormPage(token, error), StatusCodes.Status400BadRequest);
        });
    }

    private static IResult Html(string html, int statusCode) =>
        Results.Content(html, "text/html; charset=utf-8", statusCode: statusCode);

    private static string Layout(string body) => $$"""
        <!DOCTYPE html>
        <html lang="tr">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex, nofollow">
        <title>Merchant Credential Girişi</title>
        <style>
        body { font-family: -apple-system, Segoe UI, Roboto, sans-serif; background: #f5f6f8; margin: 0;
               display: flex; justify-content: center; padding: 48px 16px; }
        .card { background: #fff; border-radius: 12px; box-shadow: 0 2px 12px rgba(0,0,0,.08);
                padding: 32px; max-width: 460px; width: 100%; }
        h1 { font-size: 1.2rem; margin: 0 0 8px; }
        p { color: #555; font-size: .92rem; line-height: 1.5; }
        label { display: block; font-size: .85rem; font-weight: 600; margin: 16px 0 4px; }
        input { width: 100%; box-sizing: border-box; padding: 10px 12px; border: 1px solid #ccd;
                border-radius: 8px; font-size: .95rem; font-family: ui-monospace, monospace; }
        button { margin-top: 24px; width: 100%; padding: 12px; border: 0; border-radius: 8px;
                 background: #1a56db; color: #fff; font-size: 1rem; font-weight: 600; cursor: pointer; }
        .error { background: #fde8e8; color: #9b1c1c; border-radius: 8px; padding: 12px; font-size: .88rem; }
        .ok { background: #def7ec; color: #03543f; border-radius: 8px; padding: 12px; font-size: .9rem; }
        .warn { background: #fdf6b2; color: #723b13; border-radius: 8px; padding: 12px; font-size: .9rem; }
        </style>
        </head>
        <body><div class="card">{{body}}</div></body>
        </html>
        """;

    private static string FormPage(string token, string? error) => Layout($"""
        <h1>Merchant Credential Girişi</h1>
        <p>Ödeme sağlayıcısının teslim sayfasından aldığınız <strong>MerchantId</strong> ve
        <strong>MerchantKey</strong> değerlerini girin. Kayıt anında sağlayıcıya karşı doğrulanır.
        Bu sayfa tek kullanımlıktır ve mevcut değerleri göstermez.</p>
        {(error is null ? "" : $"""<div class="error">{error}</div>""")}
        <form method="post" action="/merchant-credentials/{token}" autocomplete="off">
          <label for="merchantId">MerchantId</label>
          <input id="merchantId" name="merchantId" required placeholder="00000000-0000-0000-0000-000000000000">
          <label for="merchantKey">MerchantKey</label>
          <input id="merchantKey" name="merchantKey" type="password" required placeholder="mk_...">
          <button type="submit">Doğrula ve Kaydet</button>
        </form>
        """);

    private static string SuccessPage(bool verified) => Layout(verified
        ? """
          <h1>Kaydedildi ✓</h1>
          <div class="ok">Merchant bilgileri ödeme sağlayıcısında doğrulandı ve kaydedildi.
          Ödeme akışları yeni anahtarla çalışacak.</div>
          <p>Bu sayfayı kapatabilirsiniz; link artık geçersizdir.</p>
          """
        : """
          <h1>Kaydedildi — doğrulanamadı</h1>
          <div class="warn">Ödeme sağlayıcısına şu an ulaşılamadığı için bilgiler
          <strong>doğrulanmadan</strong> kaydedildi. İlk ödeme öncesi değerlerin doğruluğundan emin olun.</div>
          <p>Bu sayfayı kapatabilirsiniz; link artık geçersizdir.</p>
          """);

    private const string NotFoundPage = """
        <!DOCTYPE html>
        <html lang="tr"><head><meta charset="utf-8"><meta name="robots" content="noindex, nofollow">
        <title>Sayfa bulunamadı</title></head>
        <body style="font-family: sans-serif; text-align: center; padding-top: 15vh; color: #555;">
        <h1>Sayfa bulunamadı</h1>
        <p>Aradığınız bağlantı geçersiz ya da artık kullanılamıyor.</p>
        </body></html>
        """;
}