namespace Customer.Api.Domains.MerchantInformations.Features.Agents.Commands;

// 070 US3/FR-016: DropShop onboarding BAŞVURUSU sarmalayıcısı — admin tek MCP bağlantısından
// başvurur; PG Merchant.Api MCP'sine makine kimliği SUNUCU içinde taşınır (MerchantOnboardingClient,
// anayasa sapması orada belgeli). PG erişilemezse dostane hata (teknik detay sızmaz).
// ChatAgent admin persona'sının onboarding yolu DOKUNULMADI (071'e dek paralel yaşar).
public static class AdminSubmitOnboarding
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record AdminSubmitOnboardingCommand(
        Guid UserId,
        string Type,
        string Name,
        string Email,
        string GsmNumber,
        string Address,
        string Iban,
        string ContactName,
        string ContactSurname,
        string? IdentityNumber,
        string? TaxOffice,
        string? TaxNumber,
        string? LegalCompanyTitle);

    public class AdminSubmitOnboardingResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    [Transactional]
    public class AdminSubmitOnboardingCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminSubmitOnboardingResponse>> Handle(
            AdminSubmitOnboardingCommand cmd,
            IDocumentSession session,
            Onboarding.MerchantOnboardingClient gateway,
            CancellationToken ct)
        {
            if (!gateway.IsConfigured)
                return Unavailable();

            var raw = await gateway.CallAsync("submit_registration", new Dictionary<string, object?>
            {
                ["type"] = cmd.Type,
                ["name"] = cmd.Name,
                ["email"] = cmd.Email,
                ["gsmNumber"] = cmd.GsmNumber,
                ["address"] = cmd.Address,
                ["iban"] = cmd.Iban,
                ["contactName"] = cmd.ContactName,
                ["contactSurname"] = cmd.ContactSurname,
                ["identityNumber"] = cmd.IdentityNumber,
                ["taxOffice"] = cmd.TaxOffice,
                ["taxNumber"] = cmd.TaxNumber,
                ["legalCompanyTitle"] = cmd.LegalCompanyTitle,
            }, ct);

            if (raw is null)
            {
                return Unavailable();
            }

            var parsed = OnboardingResultParser.Parse(raw);

            if (!parsed.IsSuccess)
                return FeatureObjectResultModel<AdminSubmitOnboardingResponse>.Error(new MessageItem
                {
                    Property = parsed.ErrorProperty,
                    Code = parsed.ErrorCode ?? CustomerResourceConstants.MERCHANT_ONBOARDING_UNAVAILABLE
                });

            return FeatureObjectResultModel<AdminSubmitOnboardingResponse>.Ok(new AdminSubmitOnboardingResponse
            {
                Status = parsed.Status ?? "Pending",
                Message = parsed.Message ?? "Basvuru alindi; onay bekleniyor."
            });
        }

        private static FeatureObjectResultModel<AdminSubmitOnboardingResponse> Unavailable() =>
            FeatureObjectResultModel<AdminSubmitOnboardingResponse>.Error(new MessageItem
            { Code = CustomerResourceConstants.MERCHANT_ONBOARDING_UNAVAILABLE });
    }
}

// PG yanıtı = FeatureObjectResultModel JSON'u (MCP text bloğu). Gevşek, case-insensitive çözüm:
// alan bulunamazsa null kalır — çağıran dostane varsayılana düşer. İKİ onboarding slice'ı da kullanır
// (aynı BC-içi yardımcı; agent-slice izolasyon kuralı BC'ler arasıdır, dosya paylaşımı serbest).
internal static class OnboardingResultParser
{
    public sealed record ParsedResult(
        bool IsSuccess, string? Status, string? Message, string? RejectReason,
        Guid? MerchantId, string? MerchantKey, string? ErrorCode, string? ErrorProperty, string ErrorSummary);

    public static ParsedResult Parse(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var isSuccess = GetProperty(root, "isSuccess")?.GetBoolean() ?? false;
            var data = GetProperty(root, "data");

            string? errorCode = null, errorProperty = null;
            if (GetProperty(root, "messages") is { ValueKind: JsonValueKind.Array } messages
                && messages.GetArrayLength() > 0)
            {
                var first = messages[0];
                errorCode = GetProperty(first, "code")?.GetString();
                errorProperty = GetProperty(first, "property")?.GetString();
            }

            return new ParsedResult(
                isSuccess,
                Status: data is { } d1 ? GetProperty(d1, "status")?.GetString() : null,
                Message: data is { } d2 ? GetProperty(d2, "message")?.GetString() : null,
                RejectReason: data is { } d3 ? GetProperty(d3, "rejectReason")?.GetString() : null,
                MerchantId: data is { } d4 && GetProperty(d4, "merchantId") is { ValueKind: JsonValueKind.String } mid
                            && Guid.TryParse(mid.GetString(), out var g) ? g : null,
                MerchantKey: data is { } d5 ? GetProperty(d5, "merchantKey")?.GetString() : null,
                ErrorCode: errorCode,
                ErrorProperty: errorProperty,
                ErrorSummary: errorCode ?? "unparsable response");
        }
        catch (System.Text.Json.JsonException)
        {
            return new ParsedResult(false, null, null, null, null, null, null, null, "unparsable response");
        }
    }

    private static JsonElement? GetProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var prop in element.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                return prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value;
        return null;
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
    public static Task<FeatureObjectResultModel<AdminSubmitOnboarding.AdminSubmitOnboardingResponse>> AdminSubmitOnboardingAsync(
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
        return bus.InvokeAsync<FeatureObjectResultModel<AdminSubmitOnboarding.AdminSubmitOnboardingResponse>>(
            new AdminSubmitOnboarding.AdminSubmitOnboardingCommand(
                userId, type, name, email, gsmNumber, address, iban, contactName, contactSurname,
                identityNumber, taxOffice, taxNumber, legalCompanyTitle), ct);
    }
}
