using System.Net;
using WebApp.Extensions;
using WebApp.Pages.Account.Dto;
using WebApp.Services.Refit;

namespace WebApp.Services;

public class CustomerService(
    ICustomerRefitService customerRefitService,
    ILogger<CustomerService> logger)
{
    // --- AddressBook ---

    public async Task<ServiceResult<List<AddressItemDto>>> GetAddressesAsync()
    {
        var response = await customerRefitService.GetAddressesAsync();

        // Bos defter API'de NotFound zarfi doner; UI'da bos liste (hata degil).
        if (response.StatusCode == HttpStatusCode.NotFound)
            return ServiceResult<List<AddressItemDto>>.Success(new List<AddressItemDto>());

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return ServiceResult<List<AddressItemDto>>.Error("An error occurred while getting addresses");
        }

        return ServiceResult<List<AddressItemDto>>.Success(response.Content?.Data ?? new List<AddressItemDto>());
    }

    public async Task<ServiceResult> AddAddressAsync(AddAddressRequest request)
    {
        var response = await customerRefitService.AddAddressAsync(request);
        return ToResult(response, "An error occurred while adding the address");
    }

    public async Task<ServiceResult> UpdateAddressAsync(Guid id, UpdateAddressRequest request)
    {
        var response = await customerRefitService.UpdateAddressAsync(id, request);
        return ToResult(response, "An error occurred while updating the address");
    }

    public async Task<ServiceResult> DeleteAddressAsync(Guid id)
    {
        var response = await customerRefitService.DeleteAddressAsync(id);
        return ToResult(response, "An error occurred while deleting the address");
    }

    public async Task<ServiceResult> SetDefaultAddressAsync(Guid id)
    {
        var response = await customerRefitService.SetDefaultAddressAsync(id);
        return ToResult(response, "An error occurred while setting the default address");
    }

    // --- Wallet ---

    public async Task<ServiceResult<List<CardItemDto>>> GetCardsAsync()
    {
        var response = await customerRefitService.GetCardsAsync();

        if (response.StatusCode == HttpStatusCode.NotFound)
            return ServiceResult<List<CardItemDto>>.Success(new List<CardItemDto>());

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return ServiceResult<List<CardItemDto>>.Error("An error occurred while getting cards");
        }

        return ServiceResult<List<CardItemDto>>.Success(response.Content?.Data ?? new List<CardItemDto>());
    }

    public async Task<ServiceResult> AddCardAsync(AddCardRequest request)
    {
        var response = await customerRefitService.AddCardAsync(request);
        return ToResult(response, "An error occurred while adding the card");
    }

    public async Task<ServiceResult> DeleteCardAsync(Guid id)
    {
        var response = await customerRefitService.DeleteCardAsync(id);
        return ToResult(response, "An error occurred while deleting the card");
    }

    public async Task<ServiceResult> SetDefaultCardAsync(Guid id)
    {
        var response = await customerRefitService.SetDefaultCardAsync(id);
        return ToResult(response, "An error occurred while setting the default card");
    }

    private ServiceResult ToResult(global::Refit.ApiResponse<object> response, string error)
    {
        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return ServiceResult.Error(error);
        }

        return ServiceResult.Success();
    }
}