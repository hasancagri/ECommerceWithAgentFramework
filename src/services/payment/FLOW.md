# Payment — Domain Süreci

**BC ne yapar:** Checkout sırasında bir tutar için **maket ödeme kaydı** üretir. Kart bilgisi
alınır ama YOK sayılır; yalnız `Amount` anlamlıdır. Ödeme daima Success döner, kanıt siparişe verilir.

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Süreç değişince (yeni/silinen adım-event-policy) bu dosya güncellenir; mekanik rename'i guard yakalar.

## Süreç

1. **Checkout ödemeyi önce ister.** WebApp, sipariş           `(CreatePaymentCommand)`
   yaratmadan ÖNCE kullanıcı token'ıyla REST çağrısı yapar.
2. **Kart alanları gelir ama düşer.** İstek kart no/ad/tarih   `(CreatePaymentCommandHandler)`
   taşır (PAN yok, son-4 + boş CVV); handler yalnız `Amount` okur.
3. **Pending ödeme oluşur, tutar doğrulanır.** `UserId` boş    `(Payment.Create)`
   ya da tutar ≤ 0 ise Result hatası; aksi halde Pending kayıt.
4. **Maket kabul: durum anında Success.** Dış PSP/otorizasyon  `(SetStatus → PaymentStatus)`
   yok; kayıt koşulsuz başarılı işaretlenir, saklanır.
5. **Ödeme kimliği çağırana döner.** `Id` yanıt olarak verilir; `(CreatePaymentResponse)`
   WebApp bunu siparişe `paymentId` olarak taşır (idempotency).
6. **Kullanıcı ödemelerini okur.** Kişi kendi geçmişini        `(GetAllPaymentsByUserIdQuery)`
   listeler; agent için MCP tool'u aynı slice'ı sarar.         `(GetMyPaymentsMcpTool)`

## Domain kuralları (süreci yöneten değişmezler)

- **Yalnız `Amount` gerçektir.** Kart alanları kontrat gereği alınır, domain'e girmez (PAN asla saklanmaz).
- **Maket = hep başarı.** Otorizasyon/red/iade yok; kayıt Pending doğar, hemen `PaymentStatus.Success` olur.
- **Zengin aggregate (İLKE II).** `Payment` `AggregateRoot`'tan türer; fabrika + mutator Result döner, anemik değil.
- **İzole BC (İLKE I).** Kendi `paymentDb`'si; event yaymaz, başka BC'ye erişmez. Sipariş bağı çağıranda kurulur.
- **Scope yetki (İLKE V).** Yazma `payment.write`, okuma `payment.read` scope'uyla korunur.

## Sınır (bu BC'nin dokunmadığı)

Gerçek çekim/taksit/iade, kart vault (Customer/PaymentGateway'de), sipariş/stok yok. CheckoutSaga bu
BC'yi çağırmaz — ödeme sipariş yaratımından önce WebApp tarafından tetiklenir. Yapısal PSP entegrasyonu
ayrı (Order BC'nin dış `PaymentGateway` istemcisi, 039).
