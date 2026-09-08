# Payment — Domain Süreci

**BC ne yapar:** Checkout sırasında bir tutar için **maket ödeme kaydı** üretir. Kart alanı hiç
taşımaz; yalnız `Amount` anlamlıdır. Tek-faz Charge daima Success döner, kanıt saga'ya verilir.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Checkout ödemeyi broker'la ister.** Orchestrator pivot       `(ChargePaymentCommand`
   adımında komutu kuyruktan gönderir; REST yazma ucu YOK.         ` → PaymentEventHandlers)`
2. **Aynı checkout ikinci kez ödeme yaratamaz.** Var olan kayıt
   aynı `PaymentId` ile döner (idempotent).
3. **Tutar doğrulanır, ödeme tek fazda çekilir.** `UserId` boş     `(Payment.Charge)`
   ya da tutar ≤ 0 ise Result hatası; maket kabul — koşulsuz Success.
4. **Sonuç reply kuyruğuna yayınlanır.** Başarı ya da kalıcı hata  `(PaymentCharged)`
   sınıfı döner; saga pivot kararını bununla verir.
5. **Kullanıcı ödemelerini okur.** Kişi kendi geçmişini            `(GetAllPaymentsByUserIdQuery)`
   listeler; agent için MCP tool'u aynı slice'ı sarar.             `(GetMyPaymentsMcpTool)`

## Domain kuralları (süreci yöneten değişmezler)

- **Kart alanı yoktur.** Kontrat yalnız `Amount` taşır; PAN/kart verisi bu BC'ye hiç girmez.
- **Maket = hep başarı.** Otorizasyon/red/iade yok; `Charge` kaydı koşulsuz Success üretir.
- **Zengin aggregate (İLKE II).** `Payment` `AggregateRoot`'tan türer; fabrika + mutator Result döner, anemik değil.
- **İzole BC (İLKE I).** Kendi `paymentDb`'si; event yaymaz, başka BC'ye erişmez. Sipariş bağı çağıranda kurulur.
- **Scope yetki (İLKE V).** Yazma `payment.write`, okuma `payment.read` scope'uyla korunur.

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim/taksit/iade, kart vault (Customer/PaymentGateway'de), sipariş/stok yok. Çekim YALNIZ
checkout broker komutuyla tetiklenir — kullanıcıya açık yazma ucu yok. Yapısal PSP entegrasyonu
ayrı (Order BC'nin dış `PaymentGateway` istemcisi, 039 chat yolu).
