#region

using WebApp.Pages.Order.ViewModel;

#endregion

namespace WebApp.Pages.Order.Dto;

public record GetOrderHistoryResponse(DateTime Created, decimal TotalPrice, List<OrderItemViewModel> Items);