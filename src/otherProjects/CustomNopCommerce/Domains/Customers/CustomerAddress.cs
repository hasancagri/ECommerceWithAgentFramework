namespace CustomNopCommerce.Domains.Customers;

/// <summary>
/// Adres defteri girişi — Customer aggregate'inin child entity'si. Kimliği (Id) VARDIR: varsayılan
/// fatura/teslimat adresi bu Id ile işaret edilir ve sipariş bu Id'yi referanslar (bu yüzden VO değil
/// entity). nopCommerce Address paritesi. Ülke/il Directory BC'ye aittir → <see cref="CountryId"/> opak
/// referans (Directory modülü sonra formalize eder). Mutasyon yalnız Customer metotlarından.
/// </summary>
public class CustomerAddress
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string? Company { get; private set; }
    // Ülke/il Directory BC'nin sözlüğü — opak Id (Directory modülü henüz yok).
    public Guid? CountryId { get; private set; }
    public string City { get; private set; } = default!;
    public string Address1 { get; private set; } = default!;
    public string? Address2 { get; private set; }
    public string? ZipPostalCode { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string? Email { get; private set; }

    private CustomerAddress() { }

    public static CustomerAddress Create(string firstName, string lastName, string? company, Guid? countryId,
        string city, string address1, string? address2, string? zipPostalCode, string? phoneNumber, string? email)
    {
        return new CustomerAddress
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Company = company,
            CountryId = countryId,
            City = city,
            Address1 = address1,
            Address2 = address2,
            ZipPostalCode = zipPostalCode,
            PhoneNumber = phoneNumber,
            Email = email,
        };
    }
}
