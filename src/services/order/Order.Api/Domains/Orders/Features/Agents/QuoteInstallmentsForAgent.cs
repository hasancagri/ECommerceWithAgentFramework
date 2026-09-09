namespace Order.Api.Domains.Orders.Features.Agents;

// 070 US4: taksit seçenekleri — PlaceOrderForAgent zincirinin quote'a KADAR aynısı (çekim YOK):
// sepet toplamı (gRPC, sunucu-otoritesi) + ödeme bağlamı (Customer S2S: vaultToken+merchantId) +
// PG A2A quote. Yanıt YALNIZ {installmentNumber, totalPrice} — vaultToken/buyer/merchantKey ASLA
// dönmez (FR-012). Tek okuma — AdminActionLog yazılmaz. Scope: order.read (payment.read müşteri
// demetinde zaten birlikte; [RequiredScope] tek scope alır — endpoint auth + rol demeti ikinci ağ).
public static class QuoteInstallmentsForAgent
{
    [RequiredScope(AuthorizationScopes.OrderRead)]
    public record QuoteInstallmentsQuery(Guid UserId, Guid? CardId);

    public class InstallmentOptionItem
    {
        public int InstallmentNumber { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class QuoteInstallmentsResponse
    {
        public decimal BasketTotal { get; set; }
        public List<InstallmentOptionItem> Options { get; set; } = [];
        public string? Message { get; set; }
    }

    public class QuoteInstallmentsForAgentQueryHandler(
        BasketItemsClientProxy basket,
        CustomerPaymentContextClient customer,
        PaymentAgentQuoteClient quoteClient)
    {
        public async Task<FeatureObjectResultModel<QuoteInstallmentsResponse>> Handle(
            QuoteInstallmentsQuery query, CancellationToken ct)
        {
            // 1) Sepet toplamı — sunucu-otoritesi. Fail-closed: erişilemez/boş → yönlendirici mesaj.
            var snapshot = await basket.GetItemsAsync(query.UserId, ct);
            if (!snapshot.Reachable)
                return Friendly("Su an taksit sorgulanamiyor, lutfen sonra tekrar dene.");
            if (snapshot.IsEmpty)
                return Friendly("Sepetin bos — taksit gormek icin once sepete urun ekle.");

            // 2) Ödeme bağlamı — kayıtlı kart + buyer (Customer yapısal S2S). Yoksa yönlendir.
            var ctx = await customer.GetAsync(query.UserId, query.CardId, ct);
            if (ctx is null)
                return Friendly("Kayitli kart veya varsayilan adres bulunamadi — once kart/adres ekle.");

            // 3) PG A2A quote — vault token sunucuda kalır, yanıta süzülmüş liste döner.
            var options = await quoteClient.QuoteAsync(ctx.MerchantId, ctx.VaultToken, snapshot.TotalPrice, ct);
            if (options is null)
                return Friendly("Taksit sorgusu su an yapilamiyor, lutfen sonra tekrar dene.");

            return FeatureObjectResultModel<QuoteInstallmentsResponse>.Ok(new QuoteInstallmentsResponse
            {
                BasketTotal = snapshot.TotalPrice,
                Options = options.Select(o => new InstallmentOptionItem
                {
                    InstallmentNumber = o.InstallmentNumber,
                    TotalPrice = o.TotalPrice
                }).ToList(),
            });
        }

        // İş hatası değil yönlendirme: Outcome deseni yerine mesajlı boş sonuç (chat kural-8 muadili).
        private static FeatureObjectResultModel<QuoteInstallmentsResponse> Friendly(string message) =>
            FeatureObjectResultModel<QuoteInstallmentsResponse>.Ok(new QuoteInstallmentsResponse
            {
                BasketTotal = 0,
                Options = [],
                Message = message
            });
    }
}