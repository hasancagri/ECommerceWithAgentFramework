namespace WebApp.ViewModel;

// 045: detay sayfası varyant seçici görünüm modeli.
public partial record VariantFamilyViewModel(
    List<VariantAxisViewModel> Axes,
    List<VariantMemberViewModel> Members)
{
    // Seçici yalnız ayrışan eksen VARSA çizilir; eksen yoksa ad-listesi (üye adları).
    public bool HasSelector => Axes.Count > 0 && Members.Count > 1;
    public bool HasMultipleMembers => Members.Count > 1;
}

public record VariantAxisViewModel(string Attribute, List<string> Options);

public record VariantMemberViewModel(
    Guid ProductId,
    string Name,
    decimal Price,
    bool IsInStock,
    bool IsCurrent,
    Dictionary<string, string> SpecByAttribute);

// Seçici çizim modeli: her eksen bir grup; her seçenek bir hedef üyeye çözülür.
public record AxisSelector(string Attribute, List<AxisOption> Options);

public record AxisOption(string Value, Guid? TargetProductId, bool IsInStock, bool IsCurrent);

public partial record VariantFamilyViewModel
{
    // Her eksen seçeneğini bir hedef üyeye çözer: bu eksen=değer + DİĞER eksenlerde mevcut üyeyle
    // aynı; tam eşleşme yoksa bu değeri taşıyan herhangi bir üyeye düşer (FR-005).
    public IReadOnlyList<AxisSelector> BuildSelectors()
    {
        var current = Members.First(m => m.IsCurrent);
        var selectors = new List<AxisSelector>();
        foreach (var axis in Axes)
        {
            var options = axis.Options.Select(opt =>
            {
                var target = Members.FirstOrDefault(m =>
                        m.SpecByAttribute.GetValueOrDefault(axis.Attribute) == opt
                        && Axes.Where(a => a.Attribute != axis.Attribute).All(a =>
                            m.SpecByAttribute.GetValueOrDefault(a.Attribute)
                            == current.SpecByAttribute.GetValueOrDefault(a.Attribute)))
                    ?? Members.FirstOrDefault(m =>
                        m.SpecByAttribute.GetValueOrDefault(axis.Attribute) == opt);

                return new AxisOption(opt, target?.ProductId, target?.IsInStock ?? false,
                    current.SpecByAttribute.GetValueOrDefault(axis.Attribute) == opt);
            }).ToList();
            selectors.Add(new AxisSelector(axis.Attribute, options));
        }

        return selectors;
    }
}
