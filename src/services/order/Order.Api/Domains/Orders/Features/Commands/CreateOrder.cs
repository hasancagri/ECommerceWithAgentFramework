
namespace Order.Api.Domains.Orders.Features.Commands;

// 049 sonrası sipariş oluşturma checkout orchestrator'ın broker komutuyla yapılır
// (OrderEventHandlers, Shared.CheckoutMessages.CreateOrderCommand). Buradaki REST ucu + command
// handler LEGACY idi ve söküldü; geriye yalnız PlaceOrderForAgent / PaymentAttempt /
// BasketItemsClientProxy'nin paylaştığı DTO'lar kaldı.
public static class CreateOrder
{
    public record AddressDto(string Province, string District, string Street, string ZipCode, string Line);
    // 012: Quantity varsayilan 1 (geriye-uyumlu; agent yolu adet gonderir).
    public record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity = 1);
}