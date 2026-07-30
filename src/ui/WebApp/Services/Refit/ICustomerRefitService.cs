using Refit;
using WebApp.Pages.Account.Dto;

namespace WebApp.Services.Refit;

// Customer BC (022): Wallet (kayitli kart) + AddressBook (adres defteri). Yazma customer.write,
// okuma customer.read scope'uyla; kullanici token'i handler'larla forward edilir.
public interface ICustomerRefitService
{
    // AddressBook
    [Get("/api/v1/addresses")]
    Task<ApiResponse<ListResult<AddressItemDto>>> GetAddressesAsync();

    [Post("/api/v1/addresses")]
    Task<ApiResponse<object>> AddAddressAsync(AddAddressRequest request);

    [Put("/api/v1/addresses/{id}")]
    Task<ApiResponse<object>> UpdateAddressAsync(Guid id, UpdateAddressRequest request);

    [Delete("/api/v1/addresses/{id}")]
    Task<ApiResponse<object>> DeleteAddressAsync(Guid id);

    [Post("/api/v1/addresses/{id}/default")]
    Task<ApiResponse<object>> SetDefaultAddressAsync(Guid id);

    // Wallet
    [Get("/api/v1/cards")]
    Task<ApiResponse<ListResult<CardItemDto>>> GetCardsAsync();

    [Post("/api/v1/cards")]
    Task<ApiResponse<object>> AddCardAsync(AddCardRequest request);

    [Delete("/api/v1/cards/{id}")]
    Task<ApiResponse<object>> DeleteCardAsync(Guid id);

    [Post("/api/v1/cards/{id}/default")]
    Task<ApiResponse<object>> SetDefaultCardAsync(Guid id);
}