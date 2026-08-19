namespace CustomNopCommerce.Domains.Warehouses;

/// <summary>
/// Depo — Shipping bounded context'inin aggregate kökü. Sevkiyat kalemleri hangi depodan çıktığını buna
/// referans verir. Adres opak Id'dir (Customer/Directory BC). nopCommerce Warehouse paritesi.
/// </summary>
public class Warehouse : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string? AdminComment { get; private set; }
    // Deponun adresi — Customer/Directory BC'nin sözlüğü; opak Id.
    public Guid? AddressId { get; private set; }

    private Warehouse() { }

    /// <summary>Yeni depo oluşturur. Ad guard'ı handler'da.</summary>
    /// <remarks>Handler: CreateWarehouseCommandHandler</remarks>
    public static Warehouse Create(string name, string? adminComment, Guid? addressId) =>
        new() { Name = name, AdminComment = adminComment, AddressId = addressId };

    /// <summary>Depo adını değiştirir.</summary>
    /// <remarks>Handler: (ileride UpdateWarehouse)</remarks>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = ShippingResourceConstants.WAREHOUSE_NAME_REQUIRED });
        Name = name;
        return ResultDomain.Ok();
    }
}
