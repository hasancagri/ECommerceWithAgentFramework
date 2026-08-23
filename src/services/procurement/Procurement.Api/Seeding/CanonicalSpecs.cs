namespace Procurement.Api.Seeding;

// 043: kanonik özellik registry'sinin Procurement kopyası (Catalog CatalogSpecSeedHostedService ile
// AD-hizalı — iki BC'de AYRI seed, sözleşme AD'dır; taksonomi deseni). Tedarikçi değer-eşlemeleri
// kategori-eşlemenin ikizidir: A Türkçe ham anahtar/değer, B İngilizce kullanır. Statik seed —
// çalışma anında feed'den yeni attribute/option DOĞMAZ; eşlenemeyen ham anahtar yok sayılır.
public static class CanonicalSpecs
{
    // (Attribute, [(Option, supplier-b İngilizce değer)], supplier-b İngilizce anahtar)
    private static readonly (string Attribute, (string Option, string English)[] Options, string EnglishKey)[] Registry =
    [
        ("Renk", [("Siyah", "Black"), ("Beyaz", "White"), ("Gri", "Gray"), ("Kırmızı", "Red")], "Color"),
        ("Materyal", [("Çelik", "Steel"), ("Plastik", "Plastic"), ("Cam", "Glass"), ("Ahşap", "Wood")], "Material"),
        ("Garanti Süresi", [("1 Yıl", "1 Year"), ("2 Yıl", "2 Years"), ("3 Yıl", "3 Years")], "Warranty"),
        ("Enerji Sınıfı", [("A", "A"), ("B", "B"), ("C", "C")], "Energy Class"),
    ];

    // supplier-a ham anahtar/değer = kanonik Türkçe adların kendisi (birebir).
    private static readonly Dictionary<(string Key, string Value), SpecValue> SupplierAMap =
        Registry.SelectMany(r => r.Options.Select(o =>
                ((r.Attribute, o.Option), SpecValue.Create(r.Attribute, o.Option))))
            .ToDictionary(x => x.Item1, x => x.Item2);

    // supplier-b ham anahtar/değer = İngilizce (Color/Black...).
    private static readonly Dictionary<(string Key, string Value), SpecValue> SupplierBMap =
        Registry.SelectMany(r => r.Options.Select(o =>
                ((r.EnglishKey, o.English), SpecValue.Create(r.Attribute, o.Option))))
            .ToDictionary(x => x.Item1, x => x.Item2);

    /// <summary>Ham attribute sözlüğünü tedarikçi-eşlemesiyle kanonik çiftlere çevirir; eşlenemeyen
    /// anahtar/değer YOK SAYILIR (satırı düşürmez — kategori eşleme davranışıyla aynı).</summary>
    public static IReadOnlyList<SpecValue> MapRawAttributes(
        string supplierCode, IReadOnlyDictionary<string, string>? rawAttributes)
    {
        if (rawAttributes is null || rawAttributes.Count == 0)
            return [];

        var map = supplierCode == "supplier-b" ? SupplierBMap : SupplierAMap;
        return rawAttributes
            .Select(kv => map.TryGetValue((kv.Key.Trim(), kv.Value.Trim()), out var spec) ? spec : null)
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();
    }
}
