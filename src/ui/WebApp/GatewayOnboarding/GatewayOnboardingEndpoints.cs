namespace WebApp.GatewayOnboarding;

/// <summary>
/// E1 — aday merchant sitesinin DropShop ödeme gateway'ine kayıt için sunduğu public yüzey:
/// (1) descriptor (statik kimlik beyanı), (2) domain-control challenge (sahiplik kanıtı). Ayrıca
/// dev kolaylığı: gateway'in döndürdüğü challenge değerini yerleştiren POST ucu.
///
/// Akış (iki-adım): DropShop'ta <c>submit_registration(descriptorUrl)</c> çağrılır → gateway
/// <c>ChallengeRequired{token, expectedValue, publishPath}</c> döner → bu değer buraya POST edilir →
/// <c>submit_registration</c> tekrar çağrılır → gateway senkron GET ile doğrular → Pending başvuru.
/// </summary>
public static class GatewayOnboardingEndpoints
{
    public static IEndpointRouteBuilder MapGatewayOnboarding(this IEndpointRouteBuilder app)
    {
        // Descriptor — public, statik, sırsız (config'ten). Gateway zorunlu alanları okur.
        app.MapGet("/.well-known/merchant-descriptor.json", (WebApp.Options.GatewayOnboarding opts) =>
            Results.Json(new
            {
                schemaVersion = opts.SchemaVersion,
                domain = opts.Domain,
                legalName = opts.LegalName,
                taxId = opts.TaxId,
                contactEmail = opts.ContactEmail,
                webhookUrl = opts.WebhookUrl,
                agent = new { a2aCardUrl = opts.A2aCardUrl }
            })).AllowAnonymous();

        // Challenge — gateway'in verdiği beklenen değeri düz metin döner (tek-kullanım kanıtı).
        app.MapGet("/.well-known/merchant-challenge/{token}", (string token, IChallengeStore store) =>
        {
            var value = store.Get(token);
            return value is null ? Results.NotFound() : Results.Text(value);
        }).AllowAnonymous();

        // Dev kolaylığı: gateway'in ChallengeRequired yanıtındaki {token, expectedValue}'yu yerleştir.
        // Değeri yalnız gerçek gateway bilir; yerleştirmek tek başına yetki vermez. Dev-only.
        app.MapPost("/gateway-onboarding/challenge", (SetChallengeRequest req, IChallengeStore store) =>
        {
            if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.Value))
                return Results.BadRequest(new { error = "token ve value zorunlu." });

            store.Set(req.Token.Trim(), req.Value.Trim());
            return Results.Ok(new { published = true, path = $"/.well-known/merchant-challenge/{req.Token.Trim()}" });
        }).AllowAnonymous();

        // Otomatik kayıt sürüşü: descriptorUrl'i (kendi well-known'ımız) DropShop'a gönderir, challenge'ı
        // otomatik yayınlar, Pending başvuru oluşturur. descriptorUrl config'ten veya istek origin'inden.
        app.MapPost("/gateway-onboarding/register",
            async (HttpContext http, GatewayRegistrationClient client, WebApp.Options.GatewayOnboarding opts,
                CancellationToken ct) =>
            {
                var descriptorUrl = opts.SelfDescriptorUrl;
                if (string.IsNullOrWhiteSpace(descriptorUrl))
                    descriptorUrl = $"{http.Request.Scheme}://{http.Request.Host}/.well-known/merchant-descriptor.json";

                var result = await client.RegisterAsync(descriptorUrl, ct);
                return Results.Ok(result);
            }).AllowAnonymous();

        // Aktivasyon sonrası: insan, Identity sayfasında bir kez gösterilen merchantId + MerchantKey'i
        // buraya girer (dev secret store). Charge (G5) bununla DropShop connect/token'a gider.
        app.MapPost("/gateway-onboarding/merchant-key",
            (SetMerchantKeyRequest req, IMerchantCredentialStore creds) =>
            {
                if (req.MerchantId == Guid.Empty || string.IsNullOrWhiteSpace(req.MerchantKey))
                    return Results.BadRequest(new { error = "merchantId ve merchantKey zorunlu." });

                creds.Set(req.MerchantId, req.MerchantKey.Trim());
                return Results.Ok(new { stored = true, merchantId = req.MerchantId });
            }).AllowAnonymous();

        return app;
    }

    public record SetChallengeRequest(string Token, string Value);

    public record SetMerchantKeyRequest(Guid MerchantId, string MerchantKey);
}
