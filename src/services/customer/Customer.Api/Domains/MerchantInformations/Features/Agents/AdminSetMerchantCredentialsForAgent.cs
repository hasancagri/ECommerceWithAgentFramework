namespace Customer.Api.Domains.MerchantInformations.Features.Agents;

// 070 US3: merchant kimlik upsert (agent yüzeyi) — SetMerchantInformation İKİZİ (bilinçli tekrar).
// Yanıtta/izde MerchantKey düz metin ASLA yok (iz Summary: "credentials rotated"). İz: AdminActionLog.
public static class AdminSetMerchantCredentialsForAgent
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record AdminSetMerchantCredentialsCommand(Guid UserId, Guid MerchantId, string MerchantKey);

    public class AdminSetMerchantCredentialsResponse
    {
        public bool Configured { get; set; }
        public Guid MerchantId { get; set; }
    }

    [Transactional]
    public class AdminSetMerchantCredentialsForAgentCommandHandler
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
                    session.Store(AdminAudit.AdminActionLog.Rejected(
                        cmd.UserId, CustomerAdminTools.SetMerchantCredentials, cmd.MerchantId.ToString(),
                        "credentials update rejected"));
                    return FeatureObjectResultModel<AdminSetMerchantCredentialsResponse>.Error(updated.Messages);
                }

                session.Update(existing);
                session.Store(AdminAudit.AdminActionLog.Executed(
                    cmd.UserId, CustomerAdminTools.SetMerchantCredentials, existing.MerchantId.ToString(),
                    "credentials rotated"));
                return FeatureObjectResultModel<AdminSetMerchantCredentialsResponse>.Ok(
                    new AdminSetMerchantCredentialsResponse { Configured = true, MerchantId = existing.MerchantId });
            }

            var created = MerchantInformation.Create(cmd.MerchantId, cmd.MerchantKey);
            if (!created.IsSuccess)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, CustomerAdminTools.SetMerchantCredentials, cmd.MerchantId.ToString(),
                    "credentials create rejected"));
                return FeatureObjectResultModel<AdminSetMerchantCredentialsResponse>.Error(created.Messages);
            }

            if (existing is not null)
                session.Delete(existing);
            session.Store(created.Data!);

            session.Store(AdminAudit.AdminActionLog.Executed(
                cmd.UserId, CustomerAdminTools.SetMerchantCredentials, created.Data!.MerchantId.ToString(),
                "credentials set"));

            return FeatureObjectResultModel<AdminSetMerchantCredentialsResponse>.Ok(
                new AdminSetMerchantCredentialsResponse { Configured = true, MerchantId = created.Data!.MerchantId });
        }
    }
}