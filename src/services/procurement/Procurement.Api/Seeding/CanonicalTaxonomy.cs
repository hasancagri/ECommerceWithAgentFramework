namespace Procurement.Api.Seeding;

// Kanonik Category>SubCategory ağacının Procurement kopyası (R3: iki BC'de AYRI seed — sözleşme AD'dır).
// Catalog kendi kopyasını CatalogTaxonomySeedHostedService ile kurar; adlar birebir aynı olmak zorunda
// (event kanonik ad çifti taşır, Catalog NormalizedName ile çözer — hizasızlık error queue'ya düşer).
// Tedarikçi eşleme tabloları dataset'lerdeki ham adlarla birebir hizalıdır (contracts/mock-feed-api.md):
// A "Üst/Alt" Türkçe yolunu, B İngilizce adını kullanır.
public static class CanonicalTaxonomy
{
    // (Üst kategori, Alt kategori, supplier-b İngilizce adı)
    private static readonly (string Top, string Sub, string English)[] Tree =
    [
        ("Elektronik", "Telefon", "Phones"),
        ("Elektronik", "Kulaklık", "Headphones"),
        ("Elektronik", "Televizyon", "TVs"),
        ("Ev Aletleri", "Kahve Makinesi", "Coffee Makers"),
        ("Ev Aletleri", "Süpürge", "Vacuums"),
        ("Ev Aletleri", "Blender", "Blenders"),
        ("Moda", "Çanta", "Bags"),
        ("Moda", "Cüzdan", "Wallets"),
        ("Kişisel Bakım", "Tıraş Makinesi", "Shavers"),
        ("Kişisel Bakım", "Saç Kurutma", "Hair Dryers"),
        ("Spor", "Yoga Matı", "Yoga Mats"),
        ("Spor", "Dambıl", "Dumbbells"),
        ("Ofis", "Klavye", "Keyboards"),
        ("Ofis", "Mouse", "Mice"),
    ];

    // supplier-a ham adı: "Üst/Alt" (örn. "Elektronik/Telefon").
    public static IReadOnlyList<CategoryMapping> SupplierAMappings { get; } =
        Tree.Select(t => CategoryMapping.Create($"{t.Top}/{t.Sub}", t.Top, t.Sub)).ToList();

    // supplier-b ham adı: İngilizce (örn. "Phones").
    public static IReadOnlyList<CategoryMapping> SupplierBMappings { get; } =
        Tree.Select(t => CategoryMapping.Create(t.English, t.Top, t.Sub)).ToList();
}