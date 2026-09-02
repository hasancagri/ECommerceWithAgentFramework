using Refit;
using WebApp.Pages.Basket.Dto;

namespace WebApp.Services.Refit;

public interface IBasketRefitService
{
    [Post("/api/v1/baskets/item")]
    Task<ApiResponse<object>> AddBasketItemAsync(AddBasketRequest request);

    [Get("/api/v1/baskets/user")]
    Task<ApiResponse<BasketResponse>> GetBasketsAsync();


    [Delete("/api/v1/baskets/item/{itemId}")]
    Task<ApiResponse<object>> DeleteItemAsync(Guid itemId);

    [Put("/api/v1/baskets/item/{productId}/quantity")]
    Task<ApiResponse<object>> SetQuantityAsync(Guid productId, SetQuantityRequest body);
}