namespace Customer.Api.Domains.MerchantInformations.Features.Agents;

// 070 US3: merchant kimlik durumu (agent yüzeyi) — GetMerchantInformation İKİZİ (bilinçli tekrar).
// MerchantKey HİÇBİR koşulda dönmez (maskeli durum). Kayıt yoksa configured:false döner (NotFound
// değil — agent "tanımsız" bilgisini düz veri olarak alır). Okuma — iz yazılmaz.
public static class AdminGetMerchantStatusForAgent
{
    [RequiredScope(AuthorizationScopes.MerchantCredentialsWrite)]
    public record AdminGetMerchantStatusQuery;

    public class MerchantStatusView
    {
        public bool Configured { get; set; }
        public Guid? MerchantId { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AdminGetMerchantStatusForAgentQueryHandler
    {
        public async Task<FeatureObjectResultModel<MerchantStatusView>> Handle(
            AdminGetMerchantStatusQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var merchant = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
            if (merchant is null)
                return FeatureObjectResultModel<MerchantStatusView>.Ok(new MerchantStatusView { Configured = false });

            return FeatureObjectResultModel<MerchantStatusView>.Ok(new MerchantStatusView
            {
                Configured = true,
                MerchantId = merchant.MerchantId,
                UpdatedAt = merchant.UpdatedTime ?? merchant.CreatedTime
            });
        }
    }
}