namespace WebApp.GatewayOnboarding;

/// <summary>
/// E1 — aday merchant sitesinin DropShop ödeme gateway'ine kayıt yüzeyi. 016 push-inline: başvuru
/// alanları <c>submit_registration</c> çağrısında doğrudan gönderilir (descriptor URL çekme + domain-control
/// challenge YOK). Descriptor well-known dosyası yalnız aday sitenin kendi kimlik beyanı olarak kalır
/// (gateway artık okumaz). Tek çağrı → Pending başvuru.
/// </summary>
public static class GatewayOnboardingEndpoints
{
    public static IEndpointRouteBuilder MapGatewayOnboarding(this IEndpointRouteBuilder app)
    {
        // Descriptor — public, statik, sırsız (config'ten). Aday sitenin kendi kimlik beyanı (016: gateway
        // artık okumaz; başvuru alanları doğrudan submit_registration'a gider).
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

        // Otomatik kayıt sürüşü: başvuru alanlarını (config'ten) DropShop'a gönderir → Pending başvuru.
        app.MapPost("/gateway-onboarding/register",
            async (GatewayRegistrationClient client, WebApp.Options.GatewayOnboarding opts, CancellationToken ct) =>
            {
                var result = await client.RegisterAsync(opts, ct);
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

    public record SetMerchantKeyRequest(Guid MerchantId, string MerchantKey);
}
