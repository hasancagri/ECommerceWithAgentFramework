using Customer.Api.Domains.MerchantInformations.Features.Agents;

namespace Customer.Api.Domains.MerchantInformations;

// 070: ADMIN tool'ları — YALNIZ korumalı /mcp-admin ucunda yayınlanır (Program.cs oturum filtresi);
// müşteri /mcp tool seti DEĞİŞMEZ (müşteri DCR istemcileri admin şemasını görmez — R1). Kullanıcı
// token'dan; scope katmanı handler'da [RequiredScope(MerchantCredentialsWrite)].

[McpServerToolType]
public static class AdminGetMerchantStatusMcpTool
{
    [McpServerTool(Name = Shared.CustomerAdminTools.GetMerchantStatus)]
    [Description(
        "YONETIM: odeme gateway'i merchant kimliginin durumunu doner: {configured, merchantId?, " +
        "updatedAt?}. configured=false ise kimlik tanimsizdir — once admin_submit_onboarding ile " +
        "basvur veya elindeki Id/Key'i admin_set_merchant_credentials ile kaydet. MerchantKey (sir) " +
        "HICBIR zaman donmez.")]
    public static Task<FeatureObjectResultModel<AdminGetMerchantStatusForAgent.MerchantStatusView>> AdminGetMerchantStatusAsync(
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminGetMerchantStatusForAgent.MerchantStatusView>>(
            new AdminGetMerchantStatusForAgent.AdminGetMerchantStatusQuery(), ct);
}

[McpServerToolType]
public static class AdminSetMerchantCredentialsMcpTool
{
    [McpServerTool(Name = Shared.CustomerAdminTools.SetMerchantCredentials)]
    [Description(
        "YONETIM/YAZMA: odeme gateway'i merchant kimligini (merchantId + merchantKey) kaydeder/yeniler " +
        "(upsert). Kaynak: admin_onboarding_status'un Approved yanitindaki ikili. Sonraki siparis " +
        "cekimleri BU kimlikle calisir. Yanit {configured, merchantId} — key GERI DONMEZ; denetim " +
        "izine de yazilmaz (Summary: 'credentials rotated'). Ornek: Docker-reset sonrasi kurtarma.")]
    public static Task<FeatureObjectResultModel<AdminSetMerchantCredentialsForAgent.AdminSetMerchantCredentialsResponse>> AdminSetMerchantCredentialsAsync(
        [Description("Gateway'in verdigi merchant kimligi (GUID)")] Guid merchantId,
        [Description("Gateway'in verdigi merchant anahtari (sir; yanita geri yazilmaz)")] string merchantKey,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminSetMerchantCredentialsForAgent.AdminSetMerchantCredentialsResponse>>(
            new AdminSetMerchantCredentialsForAgent.AdminSetMerchantCredentialsCommand(userId, merchantId, merchantKey), ct);
    }
}

[McpServerToolType]
public static class AdminSubmitOnboardingMcpTool
{
    [McpServerTool(Name = Shared.CustomerAdminTools.SubmitOnboarding)]
    [Description(
        "YONETIM/YAZMA: odeme gateway'ine (DropShop) merchant kayit BASVURUSU acar; makine kimligi " +
        "sunucu icinde tasinir, senin token'in dis realm'e gitmez. type: Personal | PrivateCompany | " +
        "LimitedOrJointStockCompany. Kosullu alanlar: identityNumber (TCKN) Personal+PrivateCompany " +
        "zorunlu; taxOffice PrivateCompany+LimitedOrJointStockCompany; taxNumber + legalCompanyTitle " +
        "LimitedOrJointStockCompany. email basvurunun KIMLIGIDIR — durum sorgusu ayni adresle yapilir. " +
        "Basari yaniti {status: 'Pending', message}; onay admin_onboarding_status'tan takip edilir.")]
    public static Task<FeatureObjectResultModel<AdminSubmitOnboardingForAgent.AdminSubmitOnboardingResponse>> AdminSubmitOnboardingAsync(
        [Description("Isyeri tipi: Personal | PrivateCompany | LimitedOrJointStockCompany")] string type,
        [Description("Isyeri/site adi")] string name,
        [Description("Iletisim e-postasi (basvuru kimligi)")] string email,
        [Description("Telefon (GSM)")] string gsmNumber,
        [Description("Adres")] string address,
        [Description("TR IBAN")] string iban,
        [Description("Yetkili adi")] string contactName,
        [Description("Yetkili soyadi")] string contactSurname,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        [Description("TCKN — Personal ve PrivateCompany icin zorunlu")] string? identityNumber = null,
        [Description("Vergi dairesi — PrivateCompany ve LimitedOrJointStockCompany icin zorunlu")] string? taxOffice = null,
        [Description("Vergi no — LimitedOrJointStockCompany icin zorunlu")] string? taxNumber = null,
        [Description("Ticari unvan — PrivateCompany ve LimitedOrJointStockCompany icin zorunlu")] string? legalCompanyTitle = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminSubmitOnboardingForAgent.AdminSubmitOnboardingResponse>>(
            new AdminSubmitOnboardingForAgent.AdminSubmitOnboardingCommand(
                userId, type, name, email, gsmNumber, address, iban, contactName, contactSurname,
                identityNumber, taxOffice, taxNumber, legalCompanyTitle), ct);
    }
}

[McpServerToolType]
public static class AdminOnboardingStatusMcpTool
{
    [McpServerTool(Name = Shared.CustomerAdminTools.OnboardingStatus)]
    [Description(
        "YONETIM: odeme gateway'indeki merchant basvurusunun durumunu sorgular (email = basvuruda " +
        "kullanilan adres). Yanit {status: Pending|Approved|Rejected, message, rejectReason?, " +
        "merchantId?, merchantKey?}. Approved'da donen merchantId + merchantKey ikilisini " +
        "admin_set_merchant_credentials ile HEMEN kaydet (ekransiz kurtarma yolu); key'i kullaniciya " +
        "gosterme geregi yoksa gosterme.")]
    public static Task<FeatureObjectResultModel<AdminOnboardingStatusForAgent.AdminOnboardingStatusResponse>> AdminOnboardingStatusAsync(
        [Description("Basvuruda kullanilan e-posta")] string email,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminOnboardingStatusForAgent.AdminOnboardingStatusResponse>>(
            new AdminOnboardingStatusForAgent.AdminOnboardingStatusQuery(email), ct);
}