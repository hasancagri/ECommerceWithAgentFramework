using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.PageModels;
using WebApp.Pages.Account.Dto;
using WebApp.Services;

namespace WebApp.Pages.Account;

[Authorize]
public class AddressesModel(CustomerService customerService) : BasePageModel
{
    public List<AddressItemDto> Addresses { get; set; } = new();

    [BindProperty] public AddressInput Input { get; set; } = new();

    public async Task<IActionResult> OnGet()
    {
        var result = await customerService.GetAddressesAsync();
        if (result.IsFail) return ErrorPage(result);
        Addresses = result.Data!;
        return Page();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        var result = await customerService.AddAddressAsync(
            new AddAddressRequest(Input.Province, Input.District, Input.Street, Input.ZipCode, Input.Line));
        return result.IsFail ? ErrorPage(result, "Addresses") : SuccessPage("Address added", "Addresses");
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid id)
    {
        var result = await customerService.UpdateAddressAsync(id,
            new UpdateAddressRequest(Input.Province, Input.District, Input.Street, Input.ZipCode, Input.Line));
        return result.IsFail ? ErrorPage(result, "Addresses") : SuccessPage("Address updated", "Addresses");
    }

    public async Task<IActionResult> OnGetDeleteAsync(Guid id)
    {
        var result = await customerService.DeleteAddressAsync(id);
        return result.IsFail ? ErrorPage(result, "Addresses") : SuccessPage("Address deleted", "Addresses");
    }

    public async Task<IActionResult> OnGetSetDefaultAsync(Guid id)
    {
        var result = await customerService.SetDefaultAddressAsync(id);
        return result.IsFail ? ErrorPage(result, "Addresses") : SuccessPage("Default address set", "Addresses");
    }

    public class AddressInput
    {
        public string Province { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string Line { get; set; } = string.Empty;
    }
}