namespace Customer.Api.Mcp;

// 070/078/080/085: customer merchant yönetim yüzeyi politikası (Program.cs orkestrasyon dışı). 085: ayrı
// /mcp-admin ucu öldü — merchant-admin tool'lar TEK /mcp'de, token scope'una göre budanmış görünür.
public static class CustomerAdminSurface
{
    // Admin tool'ları (müşteri çağrısında budanır — ToolScopeMap, R1). ConfigureSessionOptions okur.
    // TUZAK: yeni admin tool eklerken buraya EKLE — ad-prefix DEĞİL açık liste.
    public static readonly string[] ToolNames =
    [
        Shared.CustomerAdminTools.GetMerchantStatus, Shared.CustomerAdminTools.OnboardingStatus,
        // 078: hosted onboarding + credential-giriş ekran linki.
        Shared.CustomerAdminTools.StartOnboarding, Shared.CustomerAdminTools.RequestCredentialEntryLink,
        // 080: merchant key yenileme tetiği (yanıt yalnız reveal URL).
        Shared.CustomerAdminTools.ReissueMerchantKey,
    ];

    // 085 R1: tool→scope eşlemesi (tek scope — merchant admin ayrım yapmaz). ConfigureSessionOptions bununla budar.
    public static readonly IReadOnlyDictionary<string, string> ToolScopeMap = ToolNames
        .ToDictionary(t => t, _ => AuthorizationScopes.MerchantCredentialsWrite);
}