using Refit;

namespace WebApp.Services.Refit;

public interface IOrderRefitService
{
    //CreateOrder endpoint
    [Post("/api/v1/orders")]
    Task<ApiResponse<object>> CreateOrder(CreateOrderRequest request);

    [Get("/api/v1/orders")]
    Task<ApiResponse<List<GetOrderHistoryResponse>>> GetOrders();
}