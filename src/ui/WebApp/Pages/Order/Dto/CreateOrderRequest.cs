namespace WebApp.Pages.Order.Dto;

public record CreateOrderRequest(
    float? DiscountRate,
    AddressDto Address,
    Guid PaymentId,
    List<OrderItemDto> Items);