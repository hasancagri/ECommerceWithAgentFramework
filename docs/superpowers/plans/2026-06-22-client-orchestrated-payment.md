# Client-Orchestrated Payment (A1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Checkout'ta Order→Payment senkron çağrısını kaldırıp, client'ın önce Payment'a (kullanıcı token'ıyla) ödeme atıp dönen `paymentId` ile Order oluşturmasını sağlamak.

**Architecture:** A1 — Client orkestrasyonu. Payment tamamen bağımsız (`amount` + kart alır, `{ id }` döner). Order yalnızca `paymentId` referansı tutar ve aynı `paymentId`'nin ikinci kez kullanılmasını engeller. Bağ tek yönlü: Order → paymentId.

**Tech Stack:** .NET 10, ASP.NET Minimal API, Wolverine (mediator), Marten (Postgres), Duende IdentityServer, Refit (WebApp→servisler), .NET Aspire (orkestrasyon).

## Global Constraints

- Test projesi YOK; her görevin doğrulaması `dotnet build` (0 hata) + ilgili çalışma-zamanı kontrolü.
- UserId daima JWT `sub` claim'inden: `Common.Auths.CurrentUser.Load(httpContext.User).Id` (statik, DI gerektirmez).
- Endpoint'ler data dönerken `Results.Ok(result.Data)`; servis-servis envelope kalmadı.
- Scope modeli servis başına `read`/`write` (`AuthorizationScopes` sabitleri).
- Identity.Server config in-memory → değişiklik sonrası Identity.Server yeniden başlatılmalı; yeni scope için kullanıcı logout/login yapmalı.
- Spec: `docs/superpowers/specs/2026-06-22-client-orchestrated-payment-design.md`.

---

### Task 1: Identity.Server — scope ve client güncellemesi

**Files:**
- Modify: `src/Identity.Server/Config.cs`

**Interfaces:**
- Produces: `ecommerce.bff` client artık `payment.read` + `payment.write` scope'larını verebilir; `order.api` client'ı kaldırılır.

- [ ] **Step 1: `ecommerce.bff` AllowedScopes'a payment ekle**

`Config.cs` içinde `ecommerce.bff` client'ının `AllowedScopes` bloğunu şu hale getir:

```csharp
            AllowedScopes =
            {
                "openid", "profile", "email", "roles",
                "catalog.read", "catalog.write",
                "basket.read", "basket.write",
                "order.read", "order.write",
                "payment.read", "payment.write",
                "discount.read", "discount.write",
            },
```

- [ ] **Step 2: `order.api` service-to-service client'ını kaldır**

`Config.cs` `Clients` listesinden `order.api` ClientId'li `new Client { ... }` bloğunun tamamını (yorum satırı dahil: "Order servisinin Payment'a service-to-service ...") sil.

- [ ] **Step 3: Build**

Run: `dotnet build src/Identity.Server/Identity.Server.csproj -v q --nologo`
Expected: `0 Hata`

- [ ] **Step 4: Commit**

```bash
git add src/Identity.Server/Config.cs
git commit -m "feat(identity): allow payment scopes for bff, remove order.api client"
```

---

### Task 2: Payment.Api — OrderCode'u kaldır, userId token'dan, .Data dön

**Files:**
- Modify: `src/services/payment/Payment.Api/Domains/Payments/Payment.cs` (entity)
- Modify: `src/services/payment/Payment.Api/Domains/Payments/Features/Commands/CreatePayment.cs`
- Modify: `src/services/payment/Payment.Api/Domains/Payments/Features/Queries/GetAllPaymentsByUserId.cs` (entity'den OrderCode kalkınca derlenmesi için)

**Interfaces:**
- Produces: `POST /api/v1/payments` artık gövdede `{ cardNumber, cardHolderName, cardExpirationDate, cardSecurityNumber, amount }` alır (UserId token'dan), başarılı yanıt gövdesi `{ id: Guid }`. Auth scope: `payment.write`.

> **Cross-file etki:** `Payment` entity'sinden `OrderCode` kaldırılıyor. `GetAllPaymentsByUserId.cs` bu alanı kullanıyor (response DTO'da `OrderCode` property'si + `OrderCode = payment.OrderCode` mapping'i). Bu task derlenebilmesi için o dosyadaki **`OrderCode` property'sini ve mapping satırını da kaldırmalı.** (`GetPaymentStatus` Task 3'te tamamen siliniyor, onu burada düşünme.)

- [ ] **Step 1: Payment entity'den OrderCode'u kaldır**

`Payment.cs` içinde:
- `public string OrderCode { get; private set; } = null!;` satırını sil.
- Factory imzasını `Create(Guid userId, decimal amount)` yap; `orderCode` parametresini ve onunla ilgili validasyon bloğunu sil; nesne kurulumunda `OrderCode = orderCode,` satırını sil.

- [ ] **Step 2: CreatePaymentCommand'dan OrderCode'u çıkar**

`CreatePayment.cs` içinde komut record'unu şu hale getir (UserId kalır, token'dan set edilecek):

```csharp
    public record CreatePaymentCommand(
        Guid UserId,
        string CardNumber,
        string CardHolderName,
        string CardExpirationDate,
        string CardSecurityNumber,
        decimal Amount);
```

- [ ] **Step 3: Handler'dan OrderCode dedup'ını ve OrderCode kullanımını kaldır**

`CreatePaymentCommandHandler.Handle` içinde `exists` (mükerrer) sorgu bloğunu tamamen sil ve `Payment.Create` çağrısını güncelle:

```csharp
            var result = Payment.Create(cmd.UserId, cmd.Amount);
            if (!result.IsSuccess)
            {
                return FeatureObjectResultModel<CreatePaymentResponse>.Error(result.Messages);
            }

            result.Data!.SetStatus(PaymentStatus.Success);
            session.Store(result.Data!);

            return FeatureObjectResultModel<CreatePaymentResponse>.Ok(new CreatePaymentResponse
            {
                Id = result.Data!.Id
            });
```

(Handler imzasında `IDocumentSession session` zaten var; eğer artık `AnyAsync` kullanılmıyorsa kullanılmayan `using` kalmasın — build uyarısı önemli değil.)

- [ ] **Step 4: Endpoint — userId token'dan, dönüş .Data**

`CreatePayment.cs` endpoint'inde dosya başına `using Common.Auths;` ekle (yoksa) ve lambda'yı güncelle:

```csharp
        group.MapPost("/",
                async ([FromBody] CreatePayment.CreatePaymentCommand cmd, HttpContext httpContext, IMessageBus bus) =>
                {
                    var userId = CurrentUser.Load(httpContext.User).Id;
                    var result =
                        await bus.InvokeAsync<FeatureObjectResultModel<CreatePayment.CreatePaymentResponse>>(
                            cmd with { UserId = userId });
                    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
                })
            .WithName("CreatePayment")
            .MapToApiVersion(1, 0)
            .Produces<CreatePayment.CreatePaymentResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError)
            .RequireAuthorization(AuthorizationScopes.PaymentWrite);
```

- [ ] **Step 5: Build**

Run: `dotnet build src/services/payment/Payment.Api/Payment.Api.csproj -v q --nologo`
Expected: `0 Hata`

- [ ] **Step 6: Commit**

```bash
git add src/services/payment/Payment.Api
git commit -m "feat(payment): standalone payment (drop orderCode, userId from token, return data)"
```

---

### Task 3: Payment.Api — GetPaymentStatus'u kaldır

**Files:**
- Delete: `src/services/payment/Payment.Api/Domains/Payments/Features/Queries/GetPaymentStatus.cs`
- Modify: `src/services/payment/Payment.Api/Domains/Payments/PaymentEndpointExtension.cs`

**Interfaces:**
- Produces: `GET /api/v1/payments/status/{orderCode}` artık yok.

- [ ] **Step 1: GetPaymentStatus dosyasını sil**

```bash
git rm src/services/payment/Payment.Api/Domains/Payments/Features/Queries/GetPaymentStatus.cs
```

- [ ] **Step 2: Endpoint kaydını kaldır**

`PaymentEndpointExtension.cs` içinde `.GetPaymentStatusEndpoint()` satırını sil. Sonuç:

```csharp
        app.MapGroup("api/v{version:apiVersion}/payments").WithTags("payments").WithApiVersionSet(apiVersionSet)
            .CreatePaymentGroupItemEndpoint()
            .GetAllPaymentsByUserIdGroupItemEndpoint()
            .RequireAuthorization();
```

- [ ] **Step 3: Build**

Run: `dotnet build src/services/payment/Payment.Api/Payment.Api.csproj -v q --nologo`
Expected: `0 Hata`

- [ ] **Step 4: Commit**

```bash
git add -A src/services/payment/Payment.Api
git commit -m "feat(payment): remove GetPaymentStatus endpoint"
```

---

### Task 4: Order.Api — Payment Refit altyapısını kaldır

**Files:**
- Delete: `src/services/order/Order.Api/Contracts/Refit/` (tüm klasör: `IPaymentService.cs`, `RefitConfiguration.cs`, `ClientAuthenticatedHttpClientHandler.cs`, `AuthenticatedHttpClientHandler.cs`, `PaymentService/*`)
- Modify: Order DI/Program kaydı (`AddRefitConfigurationExtension` çağrısı)
- Modify: `src/services/order/Order.Api/appsettings.Development.json`
- Modify: ilgili Option sınıfları (`ClientSecretOption`, `AddressUrlOption`) varsa kullanılmıyorsa sil

**Interfaces:**
- Produces: Order artık hiçbir servise giden HTTP client içermez.

- [ ] **Step 1: `AddRefitConfigurationExtension` çağrısını kaldır**

`src/services/order/Order.Api/Program.cs:50` satırını sil:

```csharp
builder.Services.AddRefitConfigurationExtension(builder.Configuration);
```

İlgili `using Order.Api.Contracts.Refit;` (varsa) satırını da temizle.

- [ ] **Step 2: Contracts/Refit klasörünü sil**

```bash
git rm -r src/services/order/Order.Api/Contracts/Refit
```

- [ ] **Step 3: Kullanılmayan Option sınıflarını ve appsettings anahtarlarını temizle**

`appsettings.Development.json`'dan `AddressUrlOption` ve `ClientSecretOption` bloklarını sil (`IdentityOption` KALIR — JWT doğrulama için).
Run: `grep -rn "ClientSecretOption\|AddressUrlOption" src/services/order/Order.Api --include=*.cs | grep -v /obj/`
Çıkan option sınıf tanımlarını (yalnızca Order'a aitse) sil; başka yerde kullanılıyorsa bırak.

- [ ] **Step 4: Build**

Run: `dotnet build src/services/order/Order.Api/Order.Api.csproj -v q --nologo`
Expected: `0 Hata` (bu adımda `CreateOrder` hâlâ eski `PaymentDto`/`IPaymentService` kullanıyorsa hata verir — Task 5 ile birlikte tamamlanır. Eğer hata `IPaymentService bulunamadı` ise Task 5'e geç, sonra bu task'ı yeniden derle.)

> Not: Task 4 ve Task 5 aynı derleme birimini (Order.Api) etkiler. İkisini ardışık uygulayıp **Task 5 sonunda** tek `dotnet build` ile doğrula; ara commit'i Task 5 sonunda at.

---

### Task 5: Order.Api — CreateOrder PaymentId'ye geçsin + idempotency

**Files:**
- Modify: `src/services/order/Order.Api/Domains/Orders/Features/Commands/CreateOrder.cs`

**Interfaces:**
- Consumes: WebApp'ten `POST /api/v1/orders` gövdesi `{ discountRate, address, paymentId, items }`.
- Produces: Order, `paymentId` ile `Paid` durumunda oluşur; aynı `paymentId` ikinci kez gelirse `BadRequest`.

- [ ] **Step 1: Komut record'unu güncelle (PaymentDto → PaymentId)**

`CreateOrder.cs` içinde:
- `public record PaymentDto(...)` satırını sil.
- Komutu şu hale getir:

```csharp
    public record CreateOrderCommand(
        float? DiscountRate,
        AddressDto Address,
        Guid PaymentId,
        List<OrderItemDto> Items);
```

- [ ] **Step 2: Handler'ı payment çağrısından arındır + idempotency ekle**

`CreateOrderCommandHandler`'ı şu hale getir (constructor'dan `IPaymentService paymentService` çıkar; `IHttpContextAccessor` ve `IMessageBus` kalır):

```csharp
    [Transactional]
    public class CreateOrderCommandHandler(
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        IMessageBus bus)
    {
        public async Task<FeatureResultModel> Handle(CreateOrderCommand cmd, CancellationToken ct)
        {
            var userId = Guid.Parse(httpContextAccessor.HttpContext!.User.FindFirst("sub")!.Value);

            // Idempotency: ayni paymentId ikinci kez siparise baglanamaz.
            var alreadyUsed = await session.Query<Order>()
                .AnyAsync(o => o.PaymentId == cmd.PaymentId, ct);
            if (alreadyUsed)
                return FeatureResultModel.Error(new MessageItem
                    { Code = "This payment has already been used for an order." });

            var address = new Address(cmd.Address.Province, cmd.Address.District, cmd.Address.Street,
                cmd.Address.ZipCode, cmd.Address.Line);
            var order = Order.Create(userId, cmd.DiscountRate, address);

            foreach (var item in cmd.Items)
            {
                var addResult = order.AddOrderItem(item.ProductId, item.ProductName, item.UnitPrice);
                if (!addResult.IsSuccess) return addResult;
            }

            order.SetPaidStatus(cmd.PaymentId);
            session.Store(order);

            await bus.PublishAsync(new IntegrationEvents.OrderCreatedEvent(order.Id, userId, order.TotalPrice));
            return FeatureResultModel.Ok();
        }
    }
```

- [ ] **Step 3: Build (Task 4 + Task 5 birlikte)**

Run: `dotnet build src/services/order/Order.Api/Order.Api.csproj -v q --nologo`
Expected: `0 Hata`

- [ ] **Step 4: Commit**

```bash
git add -A src/services/order/Order.Api
git commit -m "feat(order): client-supplied paymentId, remove payment service call, idempotency"
```

---

### Task 6: WebApp — Payment Refit client + service

**Files:**
- Create: `src/ui/WebApp/Services/Refit/IPaymentRefitService.cs`
- Create: `src/ui/WebApp/Pages/Order/Dto/CreatePaymentRequest.cs`
- Create: `src/ui/WebApp/Pages/Order/Dto/PaymentResponse.cs`
- Create: `src/ui/WebApp/Services/PaymentService.cs`
- Modify: `src/ui/WebApp/Program.cs`

**Interfaces:**
- Produces: `PaymentService.CreatePayment(CreatePaymentRequest) → Task<ServiceResult<Guid>>` (paymentId).

- [ ] **Step 1: Request/Response DTO'ları**

`CreatePaymentRequest.cs`:

```csharp
namespace WebApp.Pages.Order.Dto;

public record CreatePaymentRequest(
    string CardNumber,
    string CardHolderName,
    string CardExpirationDate,
    string CardSecurityNumber,
    decimal Amount);
```

`PaymentResponse.cs`:

```csharp
namespace WebApp.Pages.Order.Dto;

public record PaymentResponse(Guid Id);
```

- [ ] **Step 2: Refit arayüzü**

`IPaymentRefitService.cs`:

```csharp
using Refit;
using WebApp.Pages.Order.Dto;

namespace WebApp.Services.Refit;

public interface IPaymentRefitService
{
    [Post("/api/v1/payments")]
    Task<ApiResponse<PaymentResponse>> CreatePaymentAsync(CreatePaymentRequest request);
}
```

- [ ] **Step 3: PaymentService**

`PaymentService.cs`:

```csharp
using WebApp.Extensions;
using WebApp.Pages.Order.Dto;
using WebApp.Services.Refit;

namespace WebApp.Services;

public class PaymentService(IPaymentRefitService paymentRefitService, ILogger<PaymentService> logger)
{
    public async Task<ServiceResult<Guid>> CreatePayment(CreatePaymentRequest request)
    {
        var response = await paymentRefitService.CreatePaymentAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return ServiceResult<Guid>.Error("An error occurred while processing the payment");
        }

        return ServiceResult<Guid>.Success(response.Content!.Id);
    }
}
```

- [ ] **Step 4: Program.cs — Refit client + DI + OIDC scope**

`Program.cs` içinde diğer `AddRefitClient` blokları yanına ekle:

```csharp
builder.Services.AddRefitClient<IPaymentRefitService>().ConfigureHttpClient(configure =>
    {
        configure.BaseAddress = new Uri("http://payment-api");
    }).AddHttpMessageHandler<AuthenticatedHttpClientHandler>()
    .AddHttpMessageHandler<ClientAuthenticatedHttpClientHandler>();
```

`PaymentService`'i DI'a ekle (diğer `AddScoped<...Service>()` satırları yanına):

```csharp
builder.Services.AddScoped<PaymentService>();
```

OIDC scope listesine (`options.Scope.Add(...)` bloğu, discount satırlarının yanına) ekle:

```csharp
        // payment
        options.Scope.Add("payment.read");
        options.Scope.Add("payment.write");
```

- [ ] **Step 5: Build**

Run: `dotnet build src/ui/WebApp/WebApp.csproj -v q --nologo`
Expected: `0 Hata`

- [ ] **Step 6: Commit**

```bash
git add -A src/ui/WebApp
git commit -m "feat(webapp): payment refit client + service + payment scopes"
```

---

### Task 7: WebApp — checkout orkestrasyonu (önce Payment, sonra Order)

**Files:**
- Modify: `src/ui/WebApp/Pages/Order/Dto/CreateOrderRequest.cs`
- Modify: `src/ui/WebApp/Services/OrderService.cs`

**Interfaces:**
- Consumes: `PaymentService.CreatePayment(...)` (Task 6), `CreateOrderViewModel.Payment` (CardNumber, CardHolderName, ExpiryDate, Cvv), `viewModel.TotalPrice`.
- Produces: `OrderService.CreateOrder(CreateOrderViewModel)` artık önce ödeme yapıp `paymentId` ile sipariş oluşturur.

- [ ] **Step 1: CreateOrderRequest'i paymentId'ye çevir**

`CreateOrderRequest.cs`'i aç; `PaymentDto` alanını `Guid PaymentId` ile değiştir. Sonuç (alan adlarını mevcut dosyaya göre koru):

```csharp
namespace WebApp.Pages.Order.Dto;

public record CreateOrderRequest(
    float? DiscountRate,
    AddressDto Address,
    Guid PaymentId,
    List<OrderItemDto> Items);
```

`PaymentDto` artık kullanılmıyorsa onu da sil (Order DTO'larından). `grep -rn "PaymentDto" src/ui/WebApp` ile kalan kullanım olmadığını doğrula.

- [ ] **Step 2: OrderService.CreateOrder — orkestrasyon**

`OrderService`'e `PaymentService paymentService` enjekte et ve `CreateOrder`'ı güncelle:

```csharp
public class OrderService(
    IOrderRefitService orderService,
    PaymentService paymentService,
    ILogger<OrderService> logger)
{
    public async Task<ServiceResult> CreateOrder(CreateOrderViewModel viewModel)
    {
        // 1) Once odeme: client dogrudan Payment'a (kullanici token'i).
        var paymentRequest = new CreatePaymentRequest(
            viewModel.Payment.CardNumber,
            viewModel.Payment.CardHolderName,
            viewModel.Payment.ExpiryDate,
            viewModel.Payment.Cvv,
            viewModel.TotalPrice);

        var paymentResult = await paymentService.CreatePayment(paymentRequest);
        if (paymentResult.IsFail)
            return ServiceResult.Error(paymentResult.Fail!);

        // 2) Sonra siparis: donen paymentId ile.
        var address = new AddressDto(viewModel.Address.Province, viewModel.Address.District,
            viewModel.Address.Street, viewModel.Address.ZipCode, viewModel.Address.Line);

        var orderItems = viewModel.OrderItems
            .Select(x => new OrderItemDto(x.ProductId, x.ProductName, x.UnitPrice))
            .ToList();

        var createOrderRequest = new CreateOrderRequest(
            viewModel.DiscountRate, address, paymentResult.Data, orderItems);

        var response = await orderService.CreateOrder(createOrderRequest);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
                return ServiceResult.FailFromProblemDetails(response.Error);

            logger.LogProblemDetails(response.Error);
            return ServiceResult.Error("An error occurred while creating the order");
        }

        return ServiceResult.Success();
    }
```

> `ServiceResult<Guid>.Data`, `.IsFail`, `.Fail` üyelerinin mevcut `ServiceResult` API'sine uyduğunu doğrula (`src/ui/WebApp/.../ServiceResult*.cs`). Uyuşmuyorsa erişimi mevcut API'ye göre düzelt.

- [ ] **Step 3: Build**

Run: `dotnet build src/ui/WebApp/WebApp.csproj -v q --nologo`
Expected: `0 Hata`

- [ ] **Step 4: Commit**

```bash
git add -A src/ui/WebApp
git commit -m "feat(webapp): checkout orchestrates payment-then-order with paymentId"
```

---

### Task 8: Uçtan uca doğrulama

**Files:** (yok — çalışma zamanı doğrulaması)

- [ ] **Step 1: Tam solution build**

Run: `dotnet build -v q --nologo`
Expected: `0 Hata`

- [ ] **Step 2: Aspire ile çalıştır**

Run: `dotnet run --project src/AppHost/AppHost.csproj`
Identity.Server dahil tüm servisler ayağa kalkana kadar bekle.

- [ ] **Step 3: Taze token al (logout/login)**

WebApp'te logout → login (yeni `payment.read`/`payment.write` scope'larını taşıyan token için). Token'ın `aud`'unda `payment.api`, `scope`'unda `payment.write` olduğunu (gerekirse) decode ederek doğrula.

- [ ] **Step 4: Checkout senaryosu**

Sepete ürün ekle → checkout formunu doldur → "Complete Payment".
Beklenen:
- Payment.Api'de yeni bir payment kaydı (Success) oluşur, `{ id }` döner.
- Order.Api'de sipariş `Paid` durumunda, `PaymentId` dolu olarak oluşur.
- `OrderCreatedEvent` publish edilir (Basket temizliği/Discount akışı eskisi gibi çalışır).

- [ ] **Step 5: Idempotency kontrolü (manuel)**

Aynı `paymentId` ile ikinci `CreateOrder` isteği (ör. HTTP client ile tekrar) → `BadRequest` ("This payment has already been used for an order.").

- [ ] **Step 6: Regresyon**

- Order geçmişi (`/Order/...`) listelenebiliyor.
- Anonim katalog/discount okuma hâlâ çalışıyor (M2M token).

---

## Notlar
- Bu plan tek bir alt-sistemi (checkout ödeme akışı) kapsar; tek implementasyon planı için uygun ölçekte.
- Repo'da henüz commit yok; commit adımları öneridir, operatör/komut sahibi onayıyla atılır.