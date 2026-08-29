namespace Customer.Api.Domains.MerchantInformations.Features.Queries;

/// <summary>
/// 049: Order.Api'nin PG cekim/reconcile'i icin merchant API key'ini YAPISAL (S2S) kanaldan cozer.
/// MerchantKey MCP/agent yuzeyine ASLA cikmaz (get_payment_context / PaymentContextView'e eklenmez) —
/// yalniz bu internal uc (makine token customer.read) doner. Order.Api bunu PG X-Api-Key olarak kullanir;
/// statik config anahtari yerine tek kaynak MerchantInformation (reset/rotate'te senkron derdi biter).
/// </summary>
public static class GetMerchantKeyInternal
{
    public record GetMerchantKeyQuery(Guid MerchantId);

    public class MerchantKeyView
    {
        public Guid MerchantId { get; set; }
        public string MerchantKey { get; set; } = string.Empty;
    }

    public class GetMerchantKeyQueryHandler
    {
        public async Task<FeatureObjectResultModel<MerchantKeyView>> Handle(
            GetMerchantKeyQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var merchant = await session.Query<MerchantInformation>()
                .FirstOrDefaultAsync(m => m.MerchantId == query.MerchantId, ct);
            if (merchant is null)
                return FeatureObjectResultModel<MerchantKeyView>.NotFound();

            return FeatureObjectResultModel<MerchantKeyView>.Ok(new MerchantKeyView
            {
                MerchantId = merchant.MerchantId,
                MerchantKey = merchant.MerchantKey
            });
        }
    }
}
