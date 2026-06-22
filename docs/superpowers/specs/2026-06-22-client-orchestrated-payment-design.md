# Client-Orchestrated Payment (A1) — Tasarım

**Tarih:** 2026-06-22
**Durum:** Onaylandı (implementasyon bekliyor)

## Amaç

Checkout akışında **Order → Payment senkron (service-to-service) çağrısını kaldırmak.**
Bunun yerine UI/client "CompletePayment" anında **doğrudan Payment.Api'ye** kendi kullanıcı
token'ıyla ödeme isteği atar, dönen `paymentId` ile ardından Order.Api'de siparişi oluşturur.

Seçilen yaklaşım: **A1 — Client orkestrasyonu, Payment tamamen bağımsız.**
- Payment, sipariş/sepet detayını bilmez (yalnızca tutar + kart).
- Order, ödemeyi bilmez; yalnızca `paymentId` referansı tutar.
- Bağ tek yönlü: `Order → paymentId`.

## Yeni Akış

```
1. UI (Pages/Order/Create.cshtml.cs OnPost)
   └─→ Payment.Api  POST /api/v1/payments  { amount, kart bilgileri }   [kullanıcı token'ı]
        └─→ { id } (paymentId) döner
2. UI
   └─→ Order.Api    POST /api/v1/orders    { items, address, discountRate, paymentId }   [kullanıcı token'ı]
        └─→ Order "Paid" durumunda oluşur, paymentId saklanır
        └─→ OrderCreatedEvent publish edilir (mevcut davranış korunur)
```

İki adım da client tarafından, sıralı olarak yürütülür. Adım 1 başarısızsa adım 2 hiç çağrılmaz.

## Servis Bazında Değişiklikler

### Payment.Api
- `CreatePaymentCommand`: `OrderCode` kaldırılır → `{ Amount, CardNumber, CardHolderName, CardExpirationDate, CardSecurityNumber }`.
- `UserId` artık **gövdeden değil, token'dan** (`sub` claim) alınır — Basket'teki `CurrentUser.Load(httpContext.User).Id` deseni.
- `Payment` entity (`Domains/Payments/.../Payment.cs`): `OrderCode` alanı ve ilgili validasyon kaldırılır; factory `Payment.Create(userId, amount)`.
- Endpoint `CreatePayment`: dönüş `Results.Ok(result.Data)` (client typed tüketiyor → `{ id }`). Auth: `payment.write`.
- `(UserId, OrderCode)` mükerrer kontrolü kaldırılır (orderCode yok). Idempotency Order tarafına taşınır.
- **`GetPaymentStatus` endpoint + handler + `GetPaymentStatusResponse` tamamen kaldırılır.**
- `GetAllPaymentsByUserId` (payment.read) korunur — kapsam dışı, dokunulmaz.

### Order.Api
- Aşağıdakiler **tamamen kaldırılır** (Order'ın tek Refit hedefi Payment'tı):
  - `Contracts/Refit/PaymentService/IPaymentService.cs`
  - `Contracts/Refit/RefitConfiguration.cs`
  - `Contracts/Refit/ClientAuthenticatedHttpClientHandler.cs`, `AuthenticatedHttpClientHandler.cs`
  - `CreatePaymentRequest` / `CreatePaymentResponse` / `CreatePaymentResponseData` contract'ları
  - `ClientSecretOption`, `AddressUrlOption.PaymentUrl` (appsettings + option sınıfı)
  - `Program.cs`/DI'da `AddRefitConfigurationExtension` çağrısı
- `CreateOrderCommand`: `PaymentDto Payment` → `Guid PaymentId`.
- `CreateOrderCommandHandler`: payment çağrısı yok → `order.SetPaidStatus(cmd.PaymentId)` + `OrderCreatedEvent` publish. `userId` yine token'dan.
- **Mükerrer ödeme kontrolü (yeni):** Handler, `cmd.PaymentId` ile daha önce bir sipariş oluşturulmuş mu kontrol eder; oluşturulmuşsa hata döner (aynı paymentId ikinci kez siparişe bağlanamaz). Order tarafında `Order.PaymentId` üzerinden sorgu.

### Identity.Server (`Config.cs`)
- `ecommerce.bff` client `AllowedScopes`'a `payment.read`, `payment.write` eklenir.
- `order.api` (service-to-service client_credentials) client'ı **kaldırılır** (artık kullanılmıyor).
- `payment.api` ApiResource ve `payment.read`/`payment.write` ApiScope'lar korunur.

### WebApp
- Yeni `Services/Refit/IPaymentRefitService` — `[Post("/api/v1/payments")]` → typed `PaymentResponse { Guid Id }`.
- Yeni `Services/PaymentService` — Refit'i sarmalar, hata loglar.
- `Program.cs`: payment için `AddRefitClient<IPaymentRefitService>` (`http://payment-api`) + `AuthenticatedHttpClientHandler` + `ClientAuthenticatedHttpClientHandler`.
- OIDC istenen scope listesine `payment.read`, `payment.write` eklenir (login scope string ~170 karakter, 300 limiti altında).
- `Pages/Order/Create.cshtml.cs` OnPost: önce `PaymentService.CreatePayment(card, totalPrice)` → `paymentId`; başarılıysa `OrderService.CreateOrder(..., paymentId)`.
- `OrderService.CreateOrder`: kart bilgisini artık Payment'a yollar; Order'a `paymentId` gider.
- `CreateOrderRequest`: `PaymentDto` → `Guid PaymentId`.

### AppHost
- `web.WithReference(paymentApi)` zaten mevcut — değişiklik yok.
- Order artık payment'a referans / `order.api` secret'ı gerektirmez; Order `appsettings`'ten `ClientSecretOption` ve `AddressUrlOption.PaymentUrl` temizlenir. `IdentityOption` (JWT doğrulama için) **korunur**.

## Kabul Edilen Tradeoff'lar

1. **Tutar güvenliği:** `amount` client tarafından gönderilir ve doğrulanmaz. Öğrenme projesi için kabul edildi. (Gerçek sistemde Payment/Order tutarı sepetten doğrulardı.)
2. **Idempotency:** `orderCode` tabanlı kontrol yerine Order, `paymentId`'nin tekilliğini garanti eder.
3. **GetPaymentStatus:** Kaldırıldı (kapsam dışı).

## Etkilenmeyenler (bilinçli)
- Diğer servislerin response/zarf konvansiyonu, scope refactor'ü, FluentValidation kaldırılması — bu çalışmadan bağımsız, dokunulmaz.
- `OrderCreatedEvent` ve onu tüketen Basket/Discount akışı aynı kalır.

## Açık Sorular
- Yok. (Tüm tradeoff kararları verildi.)