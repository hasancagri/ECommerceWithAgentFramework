namespace CustomNopCommerce.Domains.Measures;

/// <summary>
/// Ölçü birimi türü. nopCommerce MeasureDimension + MeasureWeight iki ayrı entity'ydi; burada tek Measure
/// aggregate'ine birleşti, ayrım bu enum'la (birleştirme dersi — Recommendations gibi).
/// </summary>
public enum MeasureType
{
    Dimension = 0,
    Weight = 10,
}
