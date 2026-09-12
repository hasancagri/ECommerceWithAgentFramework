# Customer — Domain Süreci

**BC ne yapar:** Kullanıcının **adres defterini** tutar (+ merchant kimliği admin kaydı). Checkout yolunun
okuduğu kayıtlı teslimat kaynağıdır. İzole BC: integration event yayınlamaz/tüketmez; kanalı MCP (chat) +
yapısal S2S REST (merchant-key). **Kart-saklama (Wallet/cüzdan) 076'da SÖKÜLDÜ** (kart yönünden vazgeçildi;
ödeme hosted-CF'e taşınacak — [[hosted-cf-checkout-pivot]]).

> Domain-önce anlatı. Sağdaki `(…)` = koda köprü. Süreç değişince güncellenir; guard rename'i yakalar.

## Süreç

1. **Adres eklenir/güncellenir/silinir + varsayılan seçilir.**       `(AddressBook.AddAddress`
   ≤1 varsayılan invariant'ı defterde tek yazmada korunur;           ` / SetDefaultAddress)`
   yüzey chat/MCP (add_address/update/remove/set_default/list).
2. **Merchant kimliği admin kaydı** (onboarding + anahtar).          `(MerchantInformation)`
   Yapısal S2S merchant-key ucu (charge tüketicisi 076'da söküldü, uç zararsız durur).

## Domain kuralları (süreci yöneten değişmezler)

- **En fazla 1 varsayılan.** `AddressBook`'ta varsayılan seçimi diğerlerini atomik temizler.
- **Kullanıcı başına tek defter.** `UserId` ile keyli; ilk yazımda tembel oluşturulur.
- **İzole BC, event yok.** Ne yayınlar ne tüketir; kanal REST/MCP.
- **Kart-saklama YOK (076).** Cüzdan/tokenize/vault söküldü; ödeme yöntemi hosted-CF (ayrı spec).

## Sınır (bu BC'nin dokunmadığı)

Ödeme/çekim yok (hosted-CF → PG), sipariş yok (Order BC). Kart verisi hiç tutulmaz.
