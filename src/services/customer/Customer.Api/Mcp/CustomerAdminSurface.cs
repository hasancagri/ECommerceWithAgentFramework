namespace Customer.Api.Mcp;

// 070/078/080: customer /mcp-admin merchant yönetim yüzeyi politikası (Program.cs orkestrasyon dışı).
public static class CustomerAdminSurface
{
    // /mcp-admin ucunda GÖRÜNECEK admin tool'ları (müşteri /mcp'de budanır — R1). ConfigureSessionOptions okur.
    // TUZAK: yeni admin tool eklerken buraya EKLE — ad-prefix DEĞİL açık liste.
    public static readonly string[] ToolNames =
    [
        Shared.CustomerAdminTools.GetMerchantStatus, Shared.CustomerAdminTools.OnboardingStatus,
        // 078: hosted onboarding + credential-giriş ekran linki.
        Shared.CustomerAdminTools.StartOnboarding, Shared.CustomerAdminTools.RequestCredentialEntryLink,
        // 080: merchant key yenileme tetiği (yanıt yalnız reveal URL).
        Shared.CustomerAdminTools.ReissueMerchantKey,
    ];
}