using Refit;

namespace WebApp.Services.Refit;

// 049: WebApp checkout girişi ayrı Checkout.Orchestrator'a POST eder (eski Order /orders + Payment
// ön-yaratımı yerine). Orchestrator iki-faz mock ödemeyi + saga'yı kendi yürütür; 202 + CheckoutId döner.
public interface ICheckoutRefitService
{
    [Post("/api/v1/checkout")]
    Task<ApiResponse<StartCheckoutResponse>> StartCheckout(StartCheckoutRequest request);
}

public record CheckoutItemRequest(Guid ProductId, int Quantity, string Name, decimal UnitPrice);

public record CheckoutAddressRequest(string Province, string District, string Street, string ZipCode, string Line);

public record StartCheckoutRequest(
    IReadOnlyList<CheckoutItemRequest> Items,
    CheckoutAddressRequest Address,
    string CardRef,
    int Installments = 1);

public record StartCheckoutResponse(Guid CheckoutId);