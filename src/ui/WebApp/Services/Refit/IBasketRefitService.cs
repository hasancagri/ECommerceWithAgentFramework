using Refit;
using WebApp.Pages.Basket.Dto;

namespace WebApp.Services.Refit;

public interface IBasketRefitService
{
    [Post("/api/v1/baskets/item")]
    Task<ApiResponse<object>> AddBasketItemAsync(AddBasketRequest request);

    [Get("/api/v1/baskets/user")]
    Task<ApiResponse<BasketResponse>> GetBasketsAsync();


    [Put("/api/v1/baskets/apply-discount-coupon")]
    Task<ApiResponse<object>> ApplyDiscountRateAsync(ApplyDiscountRateRequest applyDiscountRateRequest);


    [Delete("/api/v1/baskets/remove-discount-coupon")]
    Task<ApiResponse<object>> RemoveDiscountRateAsync();


    [Delete("/api/v1/baskets/item/{itemId}")]
    Task<ApiResponse<object>> DeleteItemAsync(Guid itemId);
}