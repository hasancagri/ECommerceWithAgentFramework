namespace Customer.Api.Domains.MerchantInformations.Features.Agents;

// 070 US3/FR-016: DropShop onboarding BAŞVURUSU sarmalayıcısı — admin tek MCP bağlantısından
// başvurur; PG Merchant.Api MCP'sine makine kimliği SUNUCU içinde taşınır (MerchantOnboardingClient,
// anayasa sapması orada belgeli). PG erişilemezse dostane hata (teknik detay sızmaz).
// ChatAgent admin persona'sının onboarding yolu DOKUNULMADI (071'e dek paralel yaşar).
public static class AdminSubmitOnboardingForAgent
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
    public class AdminSubmitOnboardingForAgentCommandHandler
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