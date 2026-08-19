namespace CustomNopCommerce.Domains.Vendors.ValueObjects;

/// <summary>
/// Satıcıya iliştirilen admin notu. Vendor aggregate'inin child'ı; ekleme yalnız Vendor.AddNote'tan.
/// nopCommerce VendorNote paritesi.
/// </summary>
public record VendorNote
{
    public string Note { get; private init; } = default!;
    public DateTime CreatedOnUtc { get; private init; }

    private VendorNote() { }

    public static VendorNote Create(string note, DateTime createdOnUtc) =>
        new() { Note = note, CreatedOnUtc = createdOnUtc };
}
