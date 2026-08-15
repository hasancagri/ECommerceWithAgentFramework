using Customer.Api.Domains.Wallets.Payments;

namespace Customer.Api.Domains.Wallets.Features.Agents;

// 033: MCP okuma — kullanicinin DEFAULT kayitli karti + verilen tutar icin gateway'den (Payment.Api
// US2) taksit seceneklerini doner (taksit sayisi + toplam tutar). Vault token/BIN DISARI CIKMAZ;
// yalniz secenek listesi. Default kart yoksa NotFound; kartin BIN'i yoksa (eski kayit) hata.
public static class GetCardInstallmentsForAgent
{
    public record GetCardInstallmentsQuery(Guid UserId, decimal Amount);

    public class InstallmentOptionView
    {
        public int InstallmentNumber { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class InstallmentOptionsView
    {
        public List<InstallmentOptionView> Options { get; set; } = new();
    }

    public class GetCardInstallmentsQueryHandler
    {
        public async Task<FeatureObjectResultModel<InstallmentOptionsView>> Handle(
            GetCardInstallmentsQuery query,
            IQuerySession session,
            IGatewayPaymentClient gateway,
            CancellationToken ct)
        {
            var wallet = await session.Query<Wallet>()
                .FirstOrDefaultAsync(x => x.UserId == query.UserId, ct);

            var card = wallet?.Cards.FirstOrDefault(c => c.IsDefault);
            if (card is null)
                return FeatureObjectResultModel<InstallmentOptionsView>.NotFound();

            if (string.IsNullOrWhiteSpace(card.Bin))
                return FeatureObjectResultModel<InstallmentOptionsView>.Error(new MessageItem
                { Property = nameof(card.Bin), Code = CustomerResourceConstants.INVALID_OPERATION_ERROR });

            var options = await gateway.GetInstallmentsAsync(card.Bin, query.Amount, ct);
            if (options is null)
                return FeatureObjectResultModel<InstallmentOptionsView>.Error(new MessageItem
                { Property = nameof(query.Amount), Code = CustomerResourceConstants.INVALID_OPERATION_ERROR });

            return FeatureObjectResultModel<InstallmentOptionsView>.Ok(new InstallmentOptionsView
            {
                Options = options
                    .Select(o => new InstallmentOptionView
                    { InstallmentNumber = o.InstallmentNumber, TotalPrice = o.TotalPrice })
                    .ToList()
            });
        }
    }
}
