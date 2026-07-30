namespace Customer.Api.Domains.AddressBooks;

// Bir kullanicinin adres defteri. UserId ile keyli (kullanici basina tek defter).
// SavedAddress koleksiyonu + "en fazla 1 varsayilan" invariant'i aggregate icinde korunur.
public class AddressBook : AggregateRoot
{
    private AddressBook() { }

    public static AddressBook Create(Guid userId) => new() { UserId = userId };

    public Guid UserId { get; private set; }

    [JsonProperty("Addresses")] private List<SavedAddress> _addresses = new();

    [JsonIgnore] public IReadOnlyList<SavedAddress> Addresses => _addresses.AsReadOnly();

    public SavedAddress AddAddress(Address value)
    {
        var address = SavedAddress.Create(value);
        _addresses.Add(address);
        return address;
    }

    public FeatureResultModel UpdateAddress(Guid addressId, Address value)
    {
        var address = _addresses.FirstOrDefault(x => x.Id == addressId);
        if (address is null) return FeatureResultModel.NotFound();
        address.Update(value);
        return FeatureResultModel.Ok();
    }

    public FeatureResultModel RemoveAddress(Guid addressId)
    {
        var address = _addresses.FirstOrDefault(x => x.Id == addressId);
        if (address is null) return FeatureResultModel.NotFound();
        _addresses.Remove(address);
        return FeatureResultModel.Ok();
    }

    // ≤1 varsayilan invariant: hedef bulunur, digerleri false, hedef true (tek yazma — atomik).
    public FeatureResultModel SetDefaultAddress(Guid addressId)
    {
        var target = _addresses.FirstOrDefault(x => x.Id == addressId);
        if (target is null) return FeatureResultModel.NotFound();

        foreach (var address in _addresses)
            address.SetDefault(address.Id == addressId);

        return FeatureResultModel.Ok();
    }
}