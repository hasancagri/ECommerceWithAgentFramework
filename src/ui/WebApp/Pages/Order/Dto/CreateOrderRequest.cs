namespace WebApp.Pages.Order.Dto;

public record CreateOrderRequest(
    AddressDto Address,
    Guid PaymentId,
    List<OrderItemDto> Items);