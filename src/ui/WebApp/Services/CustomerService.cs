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
        if (!response.IsSuccessStatusCode)
            LogEmptyAddressFields("AddAddress", request.Province, request.District, request.Street, request.ZipCode, request.Line);
        return ToResult(response, "Please fill in all address fields (province, district, street, zip code, line).");
    }

    public async Task<ServiceResult> UpdateAddressAsync(Guid id, UpdateAddressRequest request)
    {
        var response = await customerRefitService.UpdateAddressAsync(id, request);
        if (!response.IsSuccessStatusCode)
            LogEmptyAddressFields("UpdateAddress", request.Province, request.District, request.Street, request.ZipCode, request.Line);
        return ToResult(response, "Please fill in all address fields (province, district, street, zip code, line).");
    }

    // Teshis: gonderilen istekte hangi zorunlu alan(lar) bos gitti (yalniz alan adi, deger loglanmaz).
    private void LogEmptyAddressFields(string op, string province, string district, string street, string zipCode, string line)
    {
        var empty = new[]
        {
            (nameof(province), province), (nameof(district), district), (nameof(street), street),
            (nameof(zipCode), zipCode), (nameof(line), line),
        }.Where(f => string.IsNullOrWhiteSpace(f.Item2)).Select(f => f.Item1).ToList();
        if (empty.Count > 0)
            logger.LogWarning("{Op} sent empty required field(s): {Fields}", op, string.Join(", ", empty));
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
        // PCI: PAN/CVV asla loglanmaz; yalniz genel hata mesaji.
        return ToResult(response, "Card could not be added. Check the number, expiry (not past) and CVV.");
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