using Customer.Api.Domains.MerchantInformations.Features.Agents.Commands;

namespace Customer.Api.Domains.MerchantInformations.Features.Agents.Queries;

// 070 US3/FR-016: DropShop onboarding DURUM sorgusu sarmalayıcısı. Approved'da PG merchantId +
// merchantKey döner — bu, ekransız kurtarma yolunun TESLİM anıdır: admin dönen ikiliyi
// admin_set_merchant_credentials ile kaydeder (key kalıcı İZE/loga yazılmaz; yalnız tool yanıtında
// akar — bugünkü ChatAgent onboarding akışıyla aynı teslim modeli). Okuma — iz yazılmaz (FR-009).
public static class AdminOnboardingStatus
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record AdminOnboardingStatusQuery(string Email);

    public class AdminOnboardingStatusResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? RejectReason { get; set; }
        public Guid? MerchantId { get; set; }
        public string? MerchantKey { get; set; }
    }

    public class AdminOnboardingStatusQueryHandler
    {
        public async Task<FeatureObjectResultModel<AdminOnboardingStatusResponse>> Handle(
            AdminOnboardingStatusQuery query,
            Onboarding.MerchantOnboardingClient gateway,
            CancellationToken ct)
        {
            if (!gateway.IsConfigured)
                return Unavailable();

            var raw = await gateway.CallAsync("registration_status",
                new Dictionary<string, object?> { ["email"] = query.Email }, ct);
            if (raw is null)
                return Unavailable();

            var parsed = OnboardingResultParser.Parse(raw);
            if (!parsed.IsSuccess)
                return FeatureObjectResultModel<AdminOnboardingStatusResponse>.Error(new MessageItem
                {
                    Property = parsed.ErrorProperty,
                    Code = parsed.ErrorCode ?? CustomerResourceConstants.MERCHANT_ONBOARDING_UNAVAILABLE
                });

            return FeatureObjectResultModel<AdminOnboardingStatusResponse>.Ok(new AdminOnboardingStatusResponse
            {
                Status = parsed.Status ?? "Unknown",
                Message = parsed.Message ?? string.Empty,
                RejectReason = parsed.RejectReason,
                MerchantId = parsed.MerchantId,
                MerchantKey = parsed.MerchantKey
            });
        }

        private static FeatureObjectResultModel<AdminOnboardingStatusResponse> Unavailable() =>
            FeatureObjectResultModel<AdminOnboardingStatusResponse>.Error(new MessageItem
            { Code = CustomerResourceConstants.MERCHANT_ONBOARDING_UNAVAILABLE });
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
    public static Task<FeatureObjectResultModel<AdminOnboardingStatus.AdminOnboardingStatusResponse>> AdminOnboardingStatusAsync(
        [Description("Basvuruda kullanilan e-posta")] string email,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<AdminOnboardingStatus.AdminOnboardingStatusResponse>>(
            new AdminOnboardingStatus.AdminOnboardingStatusQuery(email), ct);
}
