using System.Security.Cryptography;
using System.Text;

namespace Checkout.Orchestrator.Domains.Checkout;

// 049: checkout giriş yüzü (Command tarafı). WebApp POST buraya gelir; StartCheckout yayınlanır ve
// saga doğar. Chat Agent yüzü (Features/Agents) AYNI StartCheckout'u yayınlar (yalnız handler adı
// farklı — FR-030). Kullanıcı scope'u checkout.write ile korunur; süreç arka planda ilerler (FR-027).
public static class CheckoutEndpointExtension
{
    public record CheckoutItemRequest(Guid ProductId, int Quantity, string Name, decimal UnitPrice);

    public record StartCheckoutRequest(
        IReadOnlyList<CheckoutItemRequest> Items,
        OrderAddress Address,
        string CardRef,
        int Installments = 1);

    public record StartCheckoutResponse(Guid CheckoutId);

    public static void AddCheckoutEndpoints(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/checkout").WithTags("checkout").WithApiVersionSet(apiVersionSet)
            .MapPost("/", async (
                [FromBody] StartCheckoutRequest req,
                HttpContext httpContext,
                ICurrentUser currentUser,
                IMessageBus bus) =>
            {
                var userId = currentUser.Load(httpContext.User).Id;

                if (req.Items is null || req.Items.Count == 0)
                    return Results.BadRequest(new { code = CheckoutResourceConstants.CHECKOUT_EMPTY_ITEMS });

                var checkoutId = DeriveCheckoutId(userId, req.Items);
                var amount = req.Items.Sum(i => i.UnitPrice * i.Quantity);
                var items = req.Items.Select(i => new CheckoutItem(i.ProductId, i.Quantity, i.Name, i.UnitPrice)).ToList();

                // Süreç arka planda; senkron bekleme yok (SC-007). Saga StartCheckout ile doğar.
                await bus.PublishAsync(new StartCheckout(
                    checkoutId, userId, items, amount, req.Address, req.CardRef, req.Installments));

                return Results.Accepted($"/api/v1/checkout/{checkoutId}", new StartCheckoutResponse(checkoutId));
            })
            .WithName("StartCheckout")
            .MapToApiVersion(1, 0)
            .Produces<StartCheckoutResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .RequireAuthorization(AuthorizationScopes.CheckoutWrite);
    }

    // Idempotent başlatma (FR-029): aynı kullanıcı + aynı sepet → aynı CheckoutId (deterministik).
    private static Guid DeriveCheckoutId(Guid userId, IReadOnlyList<CheckoutItemRequest> items)
    {
        var sb = new StringBuilder(userId.ToString("N"));
        foreach (var i in items.OrderBy(x => x.ProductId))
            sb.Append('|').Append(i.ProductId.ToString("N")).Append(':').Append(i.Quantity);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return new Guid(hash.AsSpan(0, 16));
    }
}