#region


#endregion

namespace WebApp.Pages.Order.Dto;

// 028: Status (1=Beklemede, 2=Onaylandı, 3=İptal) + CancelReason (resource kodu) eklendi.
// Ayrica alan adi API ile hizalandi: Created -> CreatedTime (eski ad hic bind olmuyordu).
public record GetOrderHistoryResponse(
    DateTime CreatedTime,
    decimal TotalPrice,
    int Status,
    string? CancelReason,
    List<OrderItemViewModel> Items);