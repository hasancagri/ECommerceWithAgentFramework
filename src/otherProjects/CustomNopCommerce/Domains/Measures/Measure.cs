namespace CustomNopCommerce.Domains.Measures;

/// <summary>
/// Ölçü birimi (ör. "gram" / "kilogram" / "cm" / "inç") — Directory bounded context'inin aggregate kökü.
/// nopCommerce'in MeasureDimension + MeasureWeight iki tablosunu <see cref="MeasureType"/> ile TEK aggregate'te
/// birleştirir. <see cref="Ratio"/> = birimin baz birime oranı; çevrim saf metotta (<see cref="ConvertToBase"/>).
/// </summary>
public class Measure : AggregateRoot
{
    public MeasureType Type { get; private set; }
    public string Name { get; private set; } = default!;
    public string SystemKeyword { get; private set; } = default!;
    // Baz birime oran (baz birim = 1). Ör. baz=metre ise cm.Ratio = 100 (1 m = 100 cm).
    public decimal Ratio { get; private set; }
    public int DisplayOrder { get; private set; }

    private Measure() { }

    /// <summary>Yeni ölçü birimi oluşturur. Ad/oran guard'ı handler'da.</summary>
    /// <remarks>Handler: CreateMeasureCommandHandler</remarks>
    public static Measure Create(MeasureType type, string name, string systemKeyword, decimal ratio, int displayOrder) =>
        new()
        {
            Type = type,
            Name = name,
            SystemKeyword = systemKeyword,
            Ratio = ratio,
            DisplayOrder = displayOrder,
        };

    /// <summary>Bu birimdeki değeri baz birime çevirir (değer ÷ oran). Saf hesap.</summary>
    public decimal ConvertToBase(decimal value) => Ratio == 0 ? value : value / Ratio;
}
