using Refit;
using WebApp.Pages.Basket.Dto;

namespace WebApp.Services.Refit;

public interface IDiscountRefitService
{
    [Get("/api/v1/discounts/{coupon}")]
    Task<ApiResponse<GetDiscountByCouponResponse>> GetDiscountByCoupon(string coupon);
}