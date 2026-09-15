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
            [AuthorizationScopes.CatalogWrite] = "Katalog yazma (ürün/marka/kategori)",
            [AuthorizationScopes.BasketRead] = "Sepet okuma",
            [AuthorizationScopes.BasketWrite] = "Sepet yazma",
            [AuthorizationScopes.OrderRead] = "Sipariş okuma",
            [AuthorizationScopes.OrderWrite] = "Sipariş yazma",
            [AuthorizationScopes.PaymentRead] = "Ödeme okuma",
            [AuthorizationScopes.PaymentWrite] = "Ödeme yazma",
            [AuthorizationScopes.StockWrite] = "Stok yazma (mutlak)",
            [AuthorizationScopes.StorefrontRead] = "Vitrin okuma",
            [AuthorizationScopes.CustomerRead] = "Müşteri (cüzdan/adres) okuma",
            [AuthorizationScopes.CustomerWrite] = "Müşteri (cüzdan/adres) yazma",
            [AuthorizationScopes.ReviewsWrite] = "Ürün yorumu yazma",
            [AuthorizationScopes.LibraryRead] = "Kitaplık (fiyat alarmı) okuma",
            [AuthorizationScopes.LibraryWrite] = "Kitaplık (fiyat alarmı) yazma",
            [AuthorizationScopes.ApiKeysManage] = "API anahtarı yönetimi",
            [AuthorizationScopes.IdentityRolesManage] = "Rol/scope/kullanıcı yönetimi",
            [AuthorizationScopes.PersonalizationIngest] = "Kişiselleştirme gezinme sinyali gönderimi (m2m)",
            [AuthorizationScopes.PersonalizationRead] = "Kişiselleştirme zevk profili okuma (m2m)",
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