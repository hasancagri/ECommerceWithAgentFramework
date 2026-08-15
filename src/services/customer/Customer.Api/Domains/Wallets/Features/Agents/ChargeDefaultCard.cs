using Customer.Api.Domains.Wallets.Payments;

namespace Customer.Api.Domains.Wallets.Features.Agents;

// 033: MCP islem — kullanicinin DEFAULT kayitli kartindan GERCEK cekim. Vault token Customer.Api'de
// cozulur (LLM'e/WebApp'e ASLA donmez), gateway'e merchant OAuth (payment.charge) ile gonderilir.
// Buyer alanlari kullanici profilinden + test varsayilanlari (chat akisinda siparis yok -> tek
// sentetik sepet kalemi). Yalniz paymentId + durum doner. Default kart yoksa NotFound.
public static class ChargeDefaultCardForAgent
{
    public record ChargeDefaultCardCommand(
        Guid UserId, string? CustomerName, string? CustomerEmail, string? CustomerPhone,
        decimal Amount, decimal PaidPrice, int Installment);

    public class ChargeResultView
    {
        public Guid PaymentId { get; set; }
        public string ProviderPaymentId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal PaidPrice { get; set; }
        public int Installment { get; set; }
    }

    public class ChargeDefaultCardCommandHandler
    {
        public async Task<FeatureObjectResultModel<ChargeResultView>> Handle(
            ChargeDefaultCardCommand cmd,
            IQuerySession session,
            IGatewayPaymentClient gateway,
            CancellationToken ct)
        {
            var wallet = await session.Query<Wallet>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            var card = wallet?.Cards.FirstOrDefault(c => c.IsDefault);
            if (card is null)
                return FeatureObjectResultModel<ChargeResultView>.NotFound();

            // Buyer: profil adini ad/soyad'a bol (iyzico ayri ister); bos ise test varsayilanlari.
            var fullName = string.IsNullOrWhiteSpace(cmd.CustomerName) ? "DropShop Musteri" : cmd.CustomerName!.Trim();
            var space = fullName.IndexOf(' ');
            var name = space > 0 ? fullName[..space] : fullName;
            var surname = space > 0 ? fullName[(space + 1)..] : "Musteri";
            var email = string.IsNullOrWhiteSpace(cmd.CustomerEmail) ? "musteri@dropshop.com" : cmd.CustomerEmail!.Trim();
            var gsm = string.IsNullOrWhiteSpace(cmd.CustomerPhone) ? "+905555555555" : cmd.CustomerPhone!.Trim();

            var result = await gateway.ChargeAsync(new GatewayChargeInput(
                card.Token, cmd.Amount, cmd.PaidPrice, cmd.Installment,
                name, surname, email, gsm), ct);

            if (result is null)
                return FeatureObjectResultModel<ChargeResultView>.Error(new MessageItem
                { Property = nameof(cmd.Amount), Code = CustomerResourceConstants.INVALID_OPERATION_ERROR });

            return FeatureObjectResultModel<ChargeResultView>.Ok(new ChargeResultView
            {
                PaymentId = result.PaymentId,
                ProviderPaymentId = result.ProviderPaymentId,
                Status = result.Status,
                Price = result.Price,
                PaidPrice = result.PaidPrice,
                Installment = result.Installment
            });
        }
    }
}
