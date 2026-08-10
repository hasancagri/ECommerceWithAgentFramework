using System.ComponentModel.DataAnnotations;

namespace Customer.Api.Options;

/// <summary>
/// DropShop gateway vault bağlantı config'i — appsettings section <c>DropShopVault</c>. merchantId/key
/// BURADA DEĞİL (<see cref="Customer.Api.Domains.MerchantInformations.MerchantInformation"/>'dan gelir);
/// yalnız Identity + Payment.Api vault taban adresleri. Magic-string config[...] yerine tip'li okuma.
/// </summary>
public class DropShopVaultOption
{
    /// <summary>DropShop Identity (OpenIddict) adresi; connect/token buradan.</summary>
    [Required] public string IdentityAddress { get; set; } = "";

    /// <summary>DropShop Payment.Api taban adresi (vault uçlarının host'u).</summary>
    [Required] public string VaultBaseUrl { get; set; } = "";

    /// <summary>Token endpoint IdentityAddress'ten türetilir.</summary>
    public string TokenEndpoint => $"{IdentityAddress.TrimEnd('/')}/connect/token";
}
