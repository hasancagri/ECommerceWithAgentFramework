namespace Customer.Api.Domains.Wallets.Features.Agents;

// 038: MCP (okuma-yalniz) — cekim/taksit icin odeme baglami: secilen (veya varsayilan) kartin
// vault token'i + GERCEK musteri buyer bilgisi (profil + adres defteri varsayilan adresi) +
// gateway'deki MerchantId. Vault token + merchantId ChatAgent'a doner ve A2A ile gateway'e
// tasinir (038 tasarimi — 033'un "token sizmaz" kurali bilerek degisti: token opak, yalniz
// gateway'de cozulur). MerchantId gerekli: gateway'in get_installment_options/charge_saved_card
// tool'lari onu zorunlu ister (A2A merchant-key auth ertelendi -> kimlik govdede tasinir).
// Buyer'i ChatAgent A2A istegine VERBATIM koyar; uretmez/gostermez. TCKN/ulke/IP e-ticarette
// tutulmaz -> 033 sabitleri korunur. Varsayilan adres ya da merchant kaydi yoksa NotFound.
public static class GetPaymentContextForAgent
{
    public record GetPaymentContextQuery(
        Guid UserId, string? CustomerName, string? CustomerEmail, string? CustomerPhone, Guid? CardId);

    public class PaymentContextView
    {
        public Guid MerchantId { get; set; }
        public string VaultToken { get; set; } = default!;
        public string CardBrand { get; set; } = default!;
        public string CardLast4 { get; set; } = default!;
        public bool CardIsDefault { get; set; }
        public string BuyerName { get; set; } = default!;
        public string BuyerSurname { get; set; } = default!;
        public string BuyerEmail { get; set; } = default!;
        public string BuyerGsmNumber { get; set; } = default!;
        public string BuyerIdentityNumber { get; set; } = default!;
        public string BuyerRegistrationAddress { get; set; } = default!;
        public string BuyerCity { get; set; } = default!;
        public string BuyerCountry { get; set; } = default!;
        public string BuyerIp { get; set; } = default!;
    }

    public class GetPaymentContextQueryHandler
    {
        public async Task<FeatureObjectResultModel<PaymentContextView>> Handle(
            GetPaymentContextQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var wallet = await session.Query<Wallet>()
                .FirstOrDefaultAsync(x => x.UserId == query.UserId, ct);

            // CardId verildiyse o kart (kullaniciya ait olmali — fail-closed), yoksa varsayilan.
            var card = query.CardId is { } cardId
                ? wallet?.Cards.FirstOrDefault(c => c.Id == cardId)
                : wallet?.Cards.FirstOrDefault(c => c.IsDefault);
            if (card is null)
                return FeatureObjectResultModel<PaymentContextView>.NotFound();

            // Varsayilan adres zorunlu — buyer adresi gercek veridir (038 kullanici karari).
            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == query.UserId, ct);
            var address = book?.Addresses.FirstOrDefault(a => a.IsDefault);
            if (address is null)
                return FeatureObjectResultModel<PaymentContextView>.NotFound();

            // MerchantId zorunlu — gateway tool'lari (get_installment_options/charge_saved_card) ister.
            // Tekil kayit (operator /Admin/Onboarding'ten girer); yoksa odeme yapilamaz -> NotFound.
            var merchant = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
            if (merchant is null)
                return FeatureObjectResultModel<PaymentContextView>.NotFound();

            // Buyer: profil adini ad/soyad'a bol (iyzico ayri ister); bos ise test varsayilanlari (033 mantigi).
            var fullName = string.IsNullOrWhiteSpace(query.CustomerName) ? "DropShop Musteri" : query.CustomerName!.Trim();
            var space = fullName.IndexOf(' ');
            var name = space > 0 ? fullName[..space] : fullName;
            var surname = space > 0 ? fullName[(space + 1)..] : "Musteri";
            var email = string.IsNullOrWhiteSpace(query.CustomerEmail) ? "musteri@dropshop.com" : query.CustomerEmail!.Trim();
            var gsm = string.IsNullOrWhiteSpace(query.CustomerPhone) ? "+905555555555" : query.CustomerPhone!.Trim();

            return FeatureObjectResultModel<PaymentContextView>.Ok(new PaymentContextView
            {
                MerchantId = merchant.MerchantId,
                VaultToken = card.Token,
                CardBrand = card.Brand,
                CardLast4 = card.Last4,
                CardIsDefault = card.IsDefault,
                BuyerName = name,
                BuyerSurname = surname,
                BuyerEmail = email,
                BuyerGsmNumber = gsm,
                // TCKN e-ticarette tutulmaz — sandbox kabulu (033 GatewayPaymentClient sabiti).
                BuyerIdentityNumber = "11111111111",
                BuyerRegistrationAddress = $"{address.Value.Street} {address.Value.District} {address.Value.Line}".Trim(),
                BuyerCity = address.Value.Province,
                // Ulke/IP e-ticarette tutulmaz — sandbox kabulu (033 sabitleri).
                BuyerCountry = "Turkey",
                BuyerIp = "85.34.78.112"
            });
        }
    }
}