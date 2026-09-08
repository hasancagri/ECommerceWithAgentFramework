# Customer — Domain Süreci

**BC ne yapar:** Kullanıcının **cüzdanını** (tokenize kart, PAN yok) ve **adres defterini** tutar.
Chat/checkout yolunun okuduğu kayıtlı ödeme/teslimat kaynağıdır. İzole BC: hiçbir integration
event yayınlamaz/tüketmez; tek kanalı REST (+ MCP sarmalayıcıları).

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-invariant) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Adres eklenir/güncellenir/silinir + varsayılan seçilir.**       `(AddressBook.AddAddress`
   ≤1 varsayılan invariant'ı defterde tek yazmada korunur;           ` / SetDefaultAddress)`
   yüzey chat/MCP + REST.
2. **Kayıtlı kartlar okunur.** Yalnız gösterilebilir alanlar         `(GetCards)`
   (Brand/Last4/Bin) döner; agent MCP tool'u aynı slice'ı sarar.
3. **Sipariş ödeme bağlamını yapısal kanaldan çeker.** Buyer +       `(GetPaymentContextForAgent)`
   vaultToken + varsayılan adres; Order makine token'ıyla okur (039).
4. **Kart YAZMA yüzeyi söküldü (066 sonrası).** Ekleme/silme/
   varsayılan uçları ve komutları kaldırıldı; tokenize sınırı +      `(Wallet.AddCard`
   davranış aggregate'te durur, yüzey açılırsa buradan döner.        ` / ICardTokenizer)`

## Domain kuralları (süreci yöneten değişmezler)

- **PAN/CVV asla saklanmaz (INV-3).** `SavedCard` tip düzeyinde ham PAN/CVV taşımaz; yalnız opak token + Brand/Last4/Bin.
- **Tokenize sınırın arkasında.** `ICardTokenizer` soyut; stub bugün, PaymentGateway yarın — `Wallet` kodu değişmez.
- **En fazla 1 varsayılan.** Hem `Wallet` hem `AddressBook`'ta varsayılan seçimi diğerlerini atomik olarak temizler.
- **Kullanıcı başına tek defter.** Cüzdan/adres defteri `UserId` ile keyli; ilk yazımda tembel oluşturulur.
- **İzole BC, event yok.** Ne yayınlar ne tüketir; başka BC'ye sızmaz. Tek yol = REST/MCP (chat + Order S2S).

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim/otorizasyon yok (Payment BC), sipariş yok (Order BC). Bin uzak A2A taksit sorgusu içindir
(hassas değil); ham PAN gateway sınırının ötesine geçmez.
