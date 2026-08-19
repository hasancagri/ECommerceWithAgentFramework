namespace CustomNopCommerce.Domains.Orders.ValueObjects;

/// <summary>
/// Siparişe iliştirilen not (admin dahili veya müşteriye görünür). nopCommerce OrderNote paritesi
/// (Download eki çıkarıldı). Order aggregate'inin child'ı; ekleme yalnız Order.AddNote'tan.
/// </summary>
public record OrderNote
{
    public string Note { get; private init; } = default!;
    public bool DisplayToCustomer { get; private init; }
    public DateTime CreatedOnUtc { get; private init; }

    private OrderNote() { }

    public static OrderNote Create(string note, bool displayToCustomer, DateTime createdOnUtc) =>
        new() { Note = note, DisplayToCustomer = displayToCustomer, CreatedOnUtc = createdOnUtc };
}
