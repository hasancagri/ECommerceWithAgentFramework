# Payment — Domain Süreci

**BC ne yapar:** Checkout sırasında bir tutar için **NON-3D ödeme çekimini** yürütür (075: çekim sahibi
Payment BC). Kart verisi taşımaz — kullanıcının PG kart-handle'larını + buyer'ı Customer'dan S2S çeker,
merchant API key ile PG'ye çekim yaptırır, sonucu (PG paymentId/durum) saga'ya kanıtlar.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Checkout ödemeyi broker'la ister.** Orchestrator pivot       `(ChargePaymentCommand`
   adımında komutu kuyruktan gönderir; REST yazma ucu YOK.         ` → PaymentEventHandlers)`
2. **Aynı checkout ikinci kez ödeme yaratamaz.** Var olan kayıt
   aynı `PaymentId` ile döner (idempotent).
3. **Ödeme bağlamı + merchant key S2S çekilir.** Customer'dan       `(CustomerPaymentContextClient`
   PgUserHandle + CardHandle + buyer + merchantId; yoksa kalıcı      ` / MerchantKeyClient)`
   hata (kart/adres/anahtar eksik).
4. **PG NON-3D çekim yapılır.** Handle'larla PG'ye idempotent       `(PaymentGatewayClient`
   çekim (3DS yok); Failed → kalıcı, Ambiguous → geçici (retry).     ` → PaymentEventHandlers)`
5. **Başarılı sonuç domain'e yazılır.** `UserId` boş/tutar ≤ 0 ise  `(Payment.Charge)`
   Result hatası; başarı → Success + PG paymentId (`ChargeRef`).
6. **Sonuç reply kuyruğuna yayınlanır.** Başarı / geçici / kalıcı   `(PaymentCharged)`
   hata sınıfı döner; saga pivot kararını bununla verir.
7. **Kullanıcı ödemelerini okur.** Kişi kendi geçmişini            `(GetAllPaymentsByUserIdForAgent)`
   listeler; agent için MCP tool'u aynı slice'ı sarar.             `(GetMyPaymentsMcpTool)`

## Domain kuralları (süreci yöneten değişmezler)

- **Kart verisi yoktur.** Yalnız opak PG handle'ları + Amount; PAN/CVV bu BC'ye hiç girmez.
- **Çekim gerçek (PG NON-3D).** Otorizasyon/void/iade yok (tek çekim, pivot); Failed=kalıcı, Ambiguous=geçici (retry, PG idempotent).
- **Zengin aggregate (İLKE II).** `Payment` `AggregateRoot`'tan türer; fabrika + mutator Result döner, anemik değil.
- **İzole BC (İLKE I).** Kendi `paymentDb`'si; event yaymaz. Customer bağlamı + PG çekimi sanksiyonlu S2S (makine token / API key).
- **Scope yetki (İLKE V).** Yazma `payment.write`, okuma `payment.read`; S2S okuma customer.read makine token'ı.

## Sınır (bu BC'nin dokunmadığı)

Kart verisi/vault + hosted kart ekleme (Customer + PG'de), sipariş/stok yok. Çekim YALNIZ checkout broker
komutuyla tetiklenir — kullanıcıya açık yazma ucu yok. iyzico entegrasyonu PG'nin İÇİNDE (FR-016); Payment
yalnız PG'nin çekim sözleşmesini bilir.
