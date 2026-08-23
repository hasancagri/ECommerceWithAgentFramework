# Customer — Domain Süreci

**BC ne yapar:** Kullanıcının **cüzdanını** (tokenize kart, PAN yok) ve **adres defterini** tutar.
Checkout anında WebApp'in okuduğu kayıtlı ödeme/teslimat kaynağıdır. İzole BC: hiçbir integration
event yayınlamaz/tüketmez; tek kanalı REST.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-invariant) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Kullanıcı ham kart girer (PAN + CVV).** Ham veri yalnız bu       `(AddCardCommand)`
   komutta görülür; kalıcı kuyruğa girmez, hiçbir yere yazılmaz.
2. **Son-kullanma tokenize'dan ÖNCE doğrulanır.** Geçmiş expiry       `(Wallet.IsExpiryInFuture)`
   token üretmeden reddedilir (FR-009), orphan token önlenir.
3. **Kart gateway'de tokenize edilir.** Dönen opak token +           `(ICardTokenizer.TokenizeAsync)`
   gösterilebilir alanlar (Brand/Last4/Bin); PAN/CVV DÖNMEZ.
   Başarısızsa fail-closed: hiçbir şey saklanmaz (FR-013).
4. **Yalnız token + gösterilebilir alanlar cüzdana yazılır.**        `(SavedCard.Create → Wallet.AddCard)`
   Cüzdan yoksa kullanıcı için ilk kayıtta oluşturulur.
5. **Kart varsayılan seçilir.** Hedef true, diğerleri false —        `(Wallet.SetDefaultCard)`
   aggregate ≤1 varsayılan invariant'ını tek yazmada korur.
6. **Kart silinir + token best-effort geri çekilir.** Kart          `(Wallet.RemoveCard → RevokeAsync)`
   çıkar, token gateway vault'ta iptale gönderilir (fail-open).
7. **Adres eklenir/güncellenir/silinir + varsayılan seçilir.**       `(AddressBook.AddAddress`
   Aynı ≤1 varsayılan invariant'ı adres defterinde de tutulur.       ` / SetDefaultAddress)`
8. **Checkout adres+kartı okur.** WebApp sipariş anında kayıtlı      `(GetCards / GetAddresses)`
   defteri REST ile çeker; seçilen token Order'a taşınır.

## Domain kuralları (süreci yöneten değişmezler)

- **PAN/CVV asla saklanmaz (INV-3).** `SavedCard` tip düzeyinde ham PAN/CVV taşımaz; yalnız opak token + Brand/Last4/Bin.
- **Tokenize sınırın arkasında.** `ICardTokenizer` soyut; stub bugün, PaymentGateway yarın — `Wallet` kodu değişmez.
- **En fazla 1 varsayılan.** Hem `Wallet` hem `AddressBook`'ta varsayılan seçimi diğerlerini atomik olarak temizler.
- **Kullanıcı başına tek defter.** Cüzdan/adres defteri `UserId` ile keyli; ilk yazımda tembel oluşturulur.
- **İzole BC, event yok.** Ne yayınlar ne tüketir; başka BC'ye sızmaz. Tek yol = REST (WebApp/checkout).

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim/otorizasyon yok (Payment BC), sipariş yok (Order BC). Bin uzak A2A taksit sorgusu içindir
(hassas değil); ham PAN gateway sınırının ötesine geçmez.
