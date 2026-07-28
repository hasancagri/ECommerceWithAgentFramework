using System.Net;
using WebApp.Pages.Basket.ViewModel;
using WebApp.Services.Refit;
using WebApp.Extensions;
using WebApp.Pages.Basket.Dto;


namespace WebApp.Services;

public class BasketService(
    IBasketRefitService basketRefitService,
    ILogger<BasketService> logger)
{
    public async Task<ServiceResult> CreateOrUpdateBasketAsync(AddBasketRequest request)
    {
        var responseAsResult = await basketRefitService.AddBasketItemAsync(request);

        if (!responseAsResult.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(responseAsResult.Error);
            return ServiceResult.Error("An error occurred while creating or updating the basket");
        }


        return ServiceResult.Success();
    }


    public async Task<ServiceResult<BasketViewModel>> GetBasketsAsync()
    {
        var responseAsResult = await basketRefitService.GetBasketsAsync();

        if (!responseAsResult.IsSuccessStatusCode)
        {
            if (responseAsResult.StatusCode == HttpStatusCode.NotFound)
                return ServiceResult<BasketViewModel>.Success(BasketViewModel.Empty());


            logger.LogProblemDetails(responseAsResult.Error);
            return ServiceResult<BasketViewModel>.Error("An error occurred while getting the baskets");
        }


        var basketViewModel = new BasketViewModel(
            responseAsResult.Content!.TotalPrice,
            responseAsResult.Content.Items.Select(item => new BasketItemViewModel(
                item.Id,
                item.Name,
                item.ImageUrl, item.Price,
                item.Quantity
            )).ToList(),
            responseAsResult.Content.ReservationExpiresAt,
            responseAsResult.Content.IsReservationExpired
        );

        return ServiceResult<BasketViewModel>.Success(basketViewModel);
    }


    public async Task<ServiceResult<BasketPageViewModel>> GetBasketPageViewModelAsync()
    {
        var basketsAsResult = await GetBasketsAsync();

        if (basketsAsResult.IsFail)
            return ServiceResult<BasketPageViewModel>.Error(basketsAsResult.Fail!);

        var basketPageViewModel = new BasketPageViewModel();


        basketPageViewModel.SetPrice(basketsAsResult.Data!.TotalPrice);
        basketPageViewModel.ReservationExpiresAt = basketsAsResult.Data.ReservationExpiresAt;
        basketPageViewModel.IsReservationExpired = basketsAsResult.Data.IsReservationExpired;


        foreach (var basketItem in basketsAsResult.Data!.Items)
            basketPageViewModel.Items.Add(new BasketViewModelItem(basketItem.Id, basketItem.ImageUrl,
                basketItem.Name,
                basketItem.Price,
                basketItem.Quantity));


        return ServiceResult<BasketPageViewModel>.Success(basketPageViewModel);
    }


    public async Task<ServiceResult> DeleteBasketAsync(Guid itemId)
    {
        var responseAsResult = await basketRefitService.DeleteItemAsync(itemId);

        if (!responseAsResult.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(responseAsResult.Error);
            return ServiceResult.Error("An error occurred while deleting the basket");
        }

        return ServiceResult.Success();
    }
}