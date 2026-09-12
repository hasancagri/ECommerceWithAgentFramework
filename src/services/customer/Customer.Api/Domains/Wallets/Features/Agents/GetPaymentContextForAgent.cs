namespace Customer.Api.Domains.Wallets.Features.Agents;

// 075: S2S (yalnız internal REST) — çekim için ödeme bağlamı: kullanıcının PG kart-handle'ları
// (PgUserHandle + seçilen/varsayılan CardHandle) + GERÇEK buyer bilgisi (profil + varsayılan adres) +
// MerchantId. Vault token KALKTI (038); artık PG NON-3D çekimi opak handle'larla yapılır (kart PG'de).
// Handle'lar Payment BC'ye taşınır (Order→Payment charge yolu); PAN/CVV asla. Varsayılan adres ya da
// merchant kaydı yoksa NotFound. CardHandle verilmezse varsayılan kart; o da yoksa NotFound (kart yok).
public static class GetPaymentContextForAgent
{
    public record GetPaymentContextQuery(
        Guid UserId, string? CustomerName, string? CustomerEmail, string? CustomerPhone, string? CardHandle);

    public class PaymentContextView
    {
        public Guid MerchantId { get; set; }
        public string PgUserHandle { get; set; } = default!;
        public string CardHandle { get; set; } = default!;
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

            // PG kart çapası yoksa hiç kart eklenmemiş → ödeme yapılamaz.
            if (wallet?.PgUserHandle is not { } pgUserHandle || string.IsNullOrWhiteSpace(pgUserHandle))
                return FeatureObjectResultModel<PaymentContextView>.NotFound();

            // Seçilen kart verildiyse onu, yoksa varsayılanı kullan. İkisi de yoksa NotFound.
            var cardHandle = string.IsNullOrWhiteSpace(query.CardHandle) ? wallet.DefaultCardHandle : query.CardHandle;
            if (string.IsNullOrWhiteSpace(cardHandle))
                return FeatureObjectResultModel<PaymentContextView>.NotFound();

            // Varsayilan adres zorunlu — buyer adresi gercek veridir (038 kullanici karari).
            var book = await session.Query<AddressBook>()
                .FirstOrDefaultAsync(x => x.UserId == query.UserId, ct);
            var address = book?.Addresses.FirstOrDefault(a => a.IsDefault);
            if (address is null)
                return FeatureObjectResultModel<PaymentContextView>.NotFound();

            // MerchantId zorunlu — PG çekimi merchant kimliği ister; yoksa ödeme yapılamaz.
            var merchant = await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct);
            if (merchant is null)
                return FeatureObjectResultModel<PaymentContextView>.NotFound();

            // Buyer: profil adini ad/soyad'a bol (PG ayri ister); bos ise test varsayilanlari (033 mantigi).
            var fullName = string.IsNullOrWhiteSpace(query.CustomerName) ? "DropShop Musteri" : query.CustomerName!.Trim();
            var space = fullName.IndexOf(' ');
            var name = space > 0 ? fullName[..space] : fullName;
            var surname = space > 0 ? fullName[(space + 1)..] : "Musteri";
            var email = string.IsNullOrWhiteSpace(query.CustomerEmail) ? "musteri@dropshop.com" : query.CustomerEmail!.Trim();
            var gsm = string.IsNullOrWhiteSpace(query.CustomerPhone) ? "+905555555555" : query.CustomerPhone!.Trim();

            return FeatureObjectResultModel<PaymentContextView>.Ok(new PaymentContextView
            {
                MerchantId = merchant.MerchantId,
                PgUserHandle = pgUserHandle,
                CardHandle = cardHandle!,
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
