namespace Customer.Api.Domains.MerchantInformations.Features.Agents.Commands;

// 070 US3: merchant kimlik upsert (agent yüzeyi) — SetMerchantInformation İKİZİ (bilinçli tekrar).
// Yanıtta MerchantKey düz metin ASLA yok.
public static class AdminSetMerchantCredentials
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record AdminSetMerchantCredentialsCommand(Guid UserId, Guid MerchantId, string MerchantKey);

    public class AdminSetMerchantCredentialsResponse
    {
        public bool Configured { get; set; }
        public Guid MerchantId { get; set; }
    }

    [Transactional]
    public class AdminSetMerchantCredentialsCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminSetMerchantCredentialsResponse>> Handle(
            AdminSetMerchantCredentialsCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var existing = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);

            // Aynı merchant → key güncelle. Kayıt yok ya da farklı merchant (re-onboard) → yeni oluştur.
            if (existing is not null && existing.MerchantId == cmd.MerchantId)
            {
                var updated = existing.UpdateKey(cmd.MerchantKey);
                if (!updated.IsSuccess)
                {
                    return FeatureObjectResultModel<AdminSetMerchantCredentialsResponse>.Error(updated.Messages);
                }

                session.Update(existing);
                return FeatureObjectResultModel<AdminSetMerchantCredentialsResponse>.Ok(
                    new AdminSetMerchantCredentialsResponse { Configured = true, MerchantId = existing.MerchantId });
            }

            var created = MerchantInformation.Create(cmd.MerchantId, cmd.MerchantKey);
            if (!created.IsSuccess)
            {
                return FeatureObjectResultModel<AdminSetMerchantCredentialsResponse>.Error(created.Messages);
            }

            if (existing is not null)
                session.Delete(existing);
            session.Store(created.Data!);

            return FeatureObjectResultModel<AdminSetMerchantCredentialsResponse>.Ok(
                new AdminSetMerchantCredentialsResponse { Configured = true, MerchantId = created.Data!.MerchantId });
        }
    }
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
    public static Task<FeatureObjectResultModel<AdminSetMerchantCredentials.AdminSetMerchantCredentialsResponse>> AdminSetMerchantCredentialsAsync(
        [Description("Gateway'in verdigi merchant kimligi (GUID)")] Guid merchantId,
        [Description("Gateway'in verdigi merchant anahtari (sir; yanita geri yazilmaz)")] string merchantKey,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AdminSetMerchantCredentials.AdminSetMerchantCredentialsResponse>>(
            new AdminSetMerchantCredentials.AdminSetMerchantCredentialsCommand(userId, merchantId, merchantKey), ct);
    }
}
