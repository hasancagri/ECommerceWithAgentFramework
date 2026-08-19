namespace CustomNopCommerce.Domains.Customers;

/// <summary>
/// Müşteri — Customers bounded context'inin kök aggregate'i. nopCommerce'in dev Customer god-entity'si
/// (~50 alan) burada SADELEŞTİ; en önemli karar: KİMLİK/AUTH BURAYA GİRMEZ.
/// - Username/Email-as-login, Password, FailedLoginAttempts, MFA, OTP, ExternalAuth → Identity.Server (IdP).
/// - Rol (CustomerRole) → RBAC/Identity (rol = scope demeti); bu BC rol GÖRMEZ.
/// - RewardPoints → Loyalty modülü. VatNumberStatus/Affiliate/Vendor/timezone/follow-up → deferred/drop.
/// Kalan: profil bilgisi + ADRES DEFTERİ. Adresler child; varsayılan fatura/teslimat adresi bir Id ile
/// işaret edilir — varsayılan mutlaka var olan bir adres olmalı (invariant).
/// </summary>
public class Customer : AggregateRoot
{
    public string Email { get; private set; } = default!;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? Gender { get; private set; }
    public DateTime? DateOfBirth { get; private set; }
    public string? Company { get; private set; }
    public string? Phone { get; private set; }
    public string? VatNumber { get; private set; }
    public bool Active { get; private set; }

    private readonly List<CustomerAddress> _addresses = new();
    public IReadOnlyList<CustomerAddress> Addresses => _addresses;

    public Guid? DefaultBillingAddressId { get; private set; }
    public Guid? DefaultShippingAddressId { get; private set; }

    private Customer() { }

    /// <summary>Yeni müşteri profili oluşturur (aktif doğar). E-posta guard'ı handler'da. Kimlik doğrulama
    /// Identity.Server'da yapılır — bu profil yalnız iş verisidir.</summary>
    /// <remarks>Handler: RegisterCustomerCommandHandler</remarks>
    public static Customer Create(string email, string? firstName, string? lastName)
    {
        return new Customer
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Active = true,
        };
    }

    /// <summary>Profil alanlarını günceller.</summary>
    /// <remarks>Handler: UpdateCustomerProfileCommandHandler</remarks>
    public ResultDomain UpdateProfile(string? firstName, string? lastName, string? gender,
        DateTime? dateOfBirth, string? company, string? phone, string? vatNumber)
    {
        FirstName = firstName;
        LastName = lastName;
        Gender = gender;
        DateOfBirth = dateOfBirth;
        Company = company;
        Phone = phone;
        VatNumber = vatNumber;
        return ResultDomain.Ok();
    }

    /// <summary>Adres defterine adres ekler ve üretilen adresin Id'sini döner. İlk adres otomatik varsayılan olur.</summary>
    /// <remarks>Handler: AddCustomerAddressCommandHandler</remarks>
    public ResultDomain<Guid> AddAddress(CustomerAddress address)
    {
        _addresses.Add(address);
        // İlk eklenen adres varsayılan fatura + teslimat olur (kullanıcı kolaylığı).
        if (_addresses.Count == 1)
        {
            DefaultBillingAddressId = address.Id;
            DefaultShippingAddressId = address.Id;
        }
        return ResultDomain<Guid>.Ok(address.Id);
    }

    /// <summary>Bir adresi defterden çıkarır. Çıkarılan adres varsayılansa varsayılan sıfırlanır.</summary>
    /// <remarks>Handler: RemoveCustomerAddressCommandHandler</remarks>
    public ResultDomain RemoveAddress(Guid addressId)
    {
        var address = _addresses.FirstOrDefault(a => a.Id == addressId);
        if (address is null)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(addressId), Code = CustomersResourceConstants.ADDRESS_NOT_FOUND });
        _addresses.Remove(address);
        if (DefaultBillingAddressId == addressId) DefaultBillingAddressId = null;
        if (DefaultShippingAddressId == addressId) DefaultShippingAddressId = null;
        return ResultDomain.Ok();
    }

    /// <summary>Varsayılan fatura adresini ayarlar. Adres defterde OLMALI (invariant).</summary>
    /// <remarks>Handler: SetDefaultBillingAddressCommandHandler</remarks>
    public ResultDomain SetDefaultBillingAddress(Guid addressId)
    {
        if (_addresses.All(a => a.Id != addressId))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(addressId), Code = CustomersResourceConstants.ADDRESS_NOT_FOUND });
        DefaultBillingAddressId = addressId;
        return ResultDomain.Ok();
    }

    /// <summary>Varsayılan teslimat adresini ayarlar. Adres defterde OLMALI (invariant).</summary>
    /// <remarks>Handler: SetDefaultShippingAddressCommandHandler</remarks>
    public ResultDomain SetDefaultShippingAddress(Guid addressId)
    {
        if (_addresses.All(a => a.Id != addressId))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(addressId), Code = CustomersResourceConstants.ADDRESS_NOT_FOUND });
        DefaultShippingAddressId = addressId;
        return ResultDomain.Ok();
    }

    /// <summary>Müşteriyi pasifleştirir. Zaten pasifse reddedilir.</summary>
    /// <remarks>Handler: (ileride DeactivateCustomer)</remarks>
    public ResultDomain Deactivate()
    {
        if (!Active)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(Active), Code = CustomersResourceConstants.CUSTOMER_ALREADY_INACTIVE });
        Active = false;
        return ResultDomain.Ok();
    }
}
