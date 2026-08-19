using CustomNopCommerce.Domains.Vendors.ValueObjects;

namespace CustomNopCommerce.Domains.Vendors;

/// <summary>
/// Satıcı — Vendors bounded context'inin (marketplace / çok-satıcı) aggregate kökü. Ürün buna Id ile
/// bağlanır (Catalog-Core'da Product.VendorId → Vendors diye çıkarılan parçanın hedefi; opak referans).
/// Aktiflik için AggregateRoot.IsActive yeniden kullanılır. Admin notlarını child koleksiyonda tutar.
/// nopCommerce Vendor paritesi (picture/pagesize/pricerange/meta/PM çıkarıldı — medya/vitrin/Seo başka yerde).
/// </summary>
public class Vendor : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;
    // Satıcının adresi — Directory/Customer BC'nin; opak Id.
    public Guid? AddressId { get; private set; }
    public string? AdminComment { get; private set; }
    public int DisplayOrder { get; private set; }

    private readonly List<VendorNote> _notes = new();
    public IReadOnlyList<VendorNote> Notes => _notes;

    private Vendor() { }

    /// <summary>Yeni satıcı kaydı oluşturur (aktif doğar). Ad/e-posta guard'ı handler'da.</summary>
    /// <remarks>Handler: RegisterVendorCommandHandler</remarks>
    public static Vendor Create(string name, string email, string description, Guid? addressId, int displayOrder) =>
        new()
        {
            Name = name,
            Email = email,
            Description = description,
            AddressId = addressId,
            DisplayOrder = displayOrder,
        };

    /// <summary>Satıcı profil alanlarını günceller.</summary>
    /// <remarks>Handler: (ileride UpdateVendorProfile)</remarks>
    public ResultDomain UpdateProfile(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = VendorsResourceConstants.VENDOR_NAME_REQUIRED });
        Name = name;
        Description = description;
        return ResultDomain.Ok();
    }

    /// <summary>Satıcıyı pasifleştirir. Zaten pasifse reddedilir.</summary>
    /// <remarks>Handler: (ileride DeactivateVendor)</remarks>
    public ResultDomain Deactivate()
    {
        if (!IsActive)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(IsActive), Code = VendorsResourceConstants.VENDOR_ALREADY_INACTIVE });
        IsActive = false;
        return ResultDomain.Ok();
    }

    /// <summary>Satıcıya admin notu ekler. Boş not reddedilir.</summary>
    /// <remarks>Handler: AddVendorNoteCommandHandler</remarks>
    public ResultDomain AddNote(string note, DateTime createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(note))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(note), Code = VendorsResourceConstants.VENDOR_NOTE_EMPTY });
        _notes.Add(VendorNote.Create(note, createdAtUtc));
        return ResultDomain.Ok();
    }
}
