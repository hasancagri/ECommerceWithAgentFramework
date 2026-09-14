using Payment.Api.Domains.Payments.Features.Commands;

namespace Payment.Api.Domains.Payments;

// 077: hosted-CF S2S + callback uçları. intents/live = Order.Api internal (payment.write); callback =
// PG dış webhook (scope YOK → HMAC imza; US3). REST yalnız S2S/webhook — müşteri yüzü Order.Api start_payment.
public static class PaymentIntentEndpointExtension
{
    public sealed record CreateIntentRequest(Guid OrderId, Guid UserId, string BasketRef, decimal Amount, string TxRef);
    public sealed record CallbackRequest(string TxRef, string? PgPaymentRef, string Status, string? ReasonCode);
    public sealed record LiveIntentReply(Guid PaymentIntentId, Guid OrderId, string HostedUrl);

    public static void AddPaymentIntentEndpoints(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        var group = app.MapGroup("api/v{version:apiVersion}/internal/payments")
            .WithTags("PaymentIntentInternal")
            .WithApiVersionSet(apiVersionSet);

        // Order.Api → link iste (senkron). CallbackUrl store'un kendi callback ucu (mutlak, request'ten türer).
        group.MapPost("/intents", async (
            CreateIntentRequest req, HttpRequest http, IMessageBus bus, CancellationToken ct) =>
        {
            var callbackUrl = $"{http.Scheme}://{http.Host}/api/v1/internal/payments/callback";
            var result = await bus.InvokeAsync<FeatureObjectResultModel<CreatePaymentIntent.CreatePaymentIntentResponse>>(
                new CreatePaymentIntent.CreatePaymentIntentCommand(
                    req.OrderId, req.UserId, req.BasketRef, req.Amount, req.TxRef, callbackUrl), ct);

            return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
        }).RequireAuthorization(AuthorizationScopes.PaymentWrite);

        // Re-use sorgusu: aynı kullanıcı+sepet için canlı Pending intent (order oluşturmadan önce kontrol).
        group.MapGet("/intents/live", async (
            Guid userId, string basketRef, IQuerySession session, PaymentOptions options, CancellationToken ct) =>
        {
            var candidates = await session.Query<PaymentIntent>()
                .Where(p => p.UserId == userId && p.BasketRef == basketRef
                            && p.Status == PaymentIntentStatus.Pending)
                .ToListAsync(ct);
            var live = candidates.FirstOrDefault(p => p.IsLive(options.IntentTimeoutSeconds, DateTime.UtcNow));

            return live is null
                ? Results.NotFound()
                : Results.Ok(new LiveIntentReply(live.Id, live.OrderId, live.HostedUrl));
        }).RequireAuthorization(AuthorizationScopes.PaymentWrite);

        // PG → ödeme sonucu callback (HMAC imzalı; scope YOK). Geçersiz/eksik imza → 401, handler çağrılmaz.
        group.MapPost("/callback", async (
            HttpRequest http, PaymentOptions options, IMessageBus bus, CancellationToken ct) =>
        {
            // CallbackSignatureValidator yalnız marker ITransientDependency taşır → Scrutor
            // AsImplementedInterfaces onu concrete tip olarak KAYDETMEZ; endpoint'e concrete inject
            // edilince Minimal API onu body sanıp deserialize eder → 500. PaymentOptions (singleton,
            // resolvable) inject edilir, validator inline new'lenir (saf HMAC helper'ı).
            var validator = new CallbackSignatureValidator(options);

            http.EnableBuffering();
            string rawBody;
            using (var reader = new StreamReader(http.Body, Encoding.UTF8, leaveOpen: true))
                rawBody = await reader.ReadToEndAsync(ct);
            http.Body.Position = 0;

            var signature = http.Headers["X-Signature"].ToString();
            if (!validator.IsValid(rawBody, signature))
                return Results.Unauthorized();

            CallbackRequest? body;
            try
            {
                body = JsonSerializer.Deserialize<CallbackRequest>(rawBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return Results.BadRequest();
            }
            if (body is null || string.IsNullOrWhiteSpace(body.TxRef))
                return Results.BadRequest();

            var result = await bus.InvokeAsync<FeatureResultModel>(
                new HandlePaymentCallback.HandlePaymentCallbackCommand(
                    body.TxRef, body.PgPaymentRef, body.Status, body.ReasonCode), ct);

            // TxRef bulunamadı → 404 (PG retry); aksi 200 (idempotent no-op dahil).
            return result.IsSuccess ? Results.Ok() : Results.NotFound(result);
        }).AllowAnonymous();
    }
}
