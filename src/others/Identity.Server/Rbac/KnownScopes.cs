namespace Identity.Server.Rbac;

// 030 RBAC: atanabilir scope'ların KOD-sahipli KAPALI listesi. Adlar tek kaynaktan
// (Config.AllApiScopes) gelir; açıklamalar burada katmanlanır. Admin yönetim ekranının
// checkbox kaynağı + rol→scope yazımının doğrulayıcısı. DB/ekran yeni scope ÜRETEMEZ.
public static class KnownScopes
{
    public sealed record ScopeInfo(string Name, string Description);

    // İnsan-okur açıklamalar (ad → açıklama). Eksik ad için ad kendisi açıklama olur.
    private static readonly IReadOnlyDictionary<string, string> Descriptions =
        new Dictionary<string, string>
        {
            ["catalog.write"] = "Katalog yazma (ürün/marka/kategori)",
            ["basket.read"] = "Sepet okuma",
            ["basket.write"] = "Sepet yazma",
            ["order.read"] = "Sipariş okuma",
            ["order.write"] = "Sipariş yazma",
            ["payment.read"] = "Ödeme okuma",
            ["payment.write"] = "Ödeme yazma",
            ["stock.write"] = "Stok yazma (mutlak)",
            ["storefront.read"] = "Vitrin okuma",
            ["customer.read"] = "Müşteri (cüzdan/adres) okuma",
            ["customer.write"] = "Müşteri (cüzdan/adres) yazma",
            ["reviews.write"] = "Ürün yorumu yazma",
            ["apikeys.manage"] = "API anahtarı yönetimi",
            ["identity.roles.manage"] = "Rol/scope/kullanıcı yönetimi",
            ["personalization.ingest"] = "Kişiselleştirme gezinme sinyali gönderimi (m2m)",
            ["personalization.read"] = "Kişiselleştirme zevk profili okuma (m2m)",
        };

    // Atanabilir tüm scope'lar (Config.AllApiScopes tek kaynak) + açıklama.
    public static IReadOnlyList<ScopeInfo> All { get; } =
        [.. Config.AllApiScopes.Select(n => new ScopeInfo(n, Describe(n)))];

    // Hızlı üyelik kontrolü (doğrulama için).
    public static ISet<string> Names { get; } = Config.AllApiScopes.ToHashSet();

    public static bool IsKnown(string scope) => Names.Contains(scope);

    private static string Describe(string name) =>
        Descriptions.TryGetValue(name, out var d) ? d : name;
}