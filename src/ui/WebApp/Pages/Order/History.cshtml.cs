#region

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.PageModels;
using WebApp.Pages.Order.ViewModel;
using WebApp.Services;

#endregion

namespace WebApp.Pages.Order;

[Authorize]
public class HistoryModel(OrderService orderService) : BasePageModel
{
    public List<OrderHistoryViewModel> OrderHistoryList { get; set; } = null!;

    public async Task<IActionResult> OnGet()
    {
        var response = await orderService.GetHistory();


        if (response.IsFail) return ErrorPage(response);

        OrderHistoryList = response.Data!;


        return Page();
    }
}