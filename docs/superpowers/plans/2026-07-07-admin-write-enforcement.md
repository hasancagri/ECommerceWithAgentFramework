# Faz 2 — Admin-only Write Enforcement — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** catalog/discount/stock write işlemlerini, mevcut scope kontrolüne ek olarak `Admin` rolü gerektirecek şekilde handler seviyesinde korumak.

**Architecture:** `[RequiredScope]`/`ScopeAuthorizationMiddleware` desenine paralel bir `[RequiredRole]`/`RoleAuthorizationMiddleware` Common'a eklenir. Write mesaj record'larına `[RequiredRole(Roles.Admin)]` konur; her serviste Wolverine policy ile middleware weave edilir. REST + MCP tek noktadan (handler) korunur.

**Tech Stack:** .NET 10, WolverineFx (middleware policy), ASP.NET Core JWT (`MapInboundClaims=false` → ham `role` claim).

## Global Constraints

- Yalnızca catalog, discount, stock write'ları; basket/order/payment'a DOKUNULMAZ.
- Scope katmanına dokunulmaz; rol katmanı EKLENİR (ikisi de geçmeli).
- Rol kontrolü `HttpContext.User.HasClaim("role", role)` ile (scope middleware ile aynı semantik).
- Yetkisiz → `UnauthorizedAccessException` → mevcut GlobalExceptionHandler → 403.
- Rol adları `Common.Utils.Constants.Roles` sabitinden; magic string yok.
- Kod yorumları Türkçe.
- Test altyapısı yok → doğrulama `dotnet build` + manuel Aspire.

---

### Task 1: Common — Roles + RequiredRole + RoleAuthorizationMiddleware

**Files:**
- Create: `src/Common/Utils/Constants/Roles.cs`
- Create: `src/Common/Utils/Authorization/RequiredRoleAttribute.cs`
- Create: `src/Common/Utils/Authorization/RoleAuthorizationMiddleware.cs`
- Modify: `src/Identity.Server/Roles.cs` (çapraz-referans yorumu)

**Interfaces:**
- Produces:
  - `Common.Utils.Constants.Roles.Admin` (`"Admin"`), `.Customer` (`"Customer"`)
  - `Common.Utils.Authorization.RequiredRoleAttribute(string role)` — `.Role`
  - `Common.Utils.Authorization.RoleAuthorizationMiddleware.Before(Envelope, IHttpContextAccessor)`

- [ ] **Step 1: `Roles.cs` oluştur**

```csharp
namespace Common.Utils.Constants;

// Uygulama geneli rol adlari. AuthorizationScopes'un yanindaki auth-sabit ailesi.
// NOT: Identity.Server/Roles.cs ile ayni degerleri tasir; ikisi senkron kalmali.
public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}
```

- [ ] **Step 2: `RequiredRoleAttribute.cs` oluştur**

```csharp
namespace Common.Utils.Authorization;

// Bir komut/sorgu'nun (Wolverine message) calismasi icin gereken rolu isaretler.
// RoleAuthorizationMiddleware bunu okuyup token'daki "role" claim'i ile karsilastirir.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequiredRoleAttribute(string role) : Attribute
{
    public string Role { get; } = role;
}
```

- [ ] **Step 3: `RoleAuthorizationMiddleware.cs` oluştur**

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace Common.Utils.Authorization;

// Wolverine middleware: her handler'dan ONCE calisir. Mesaj tipinde [RequiredRole] varsa,
// forward edilen token'daki "role" claim'ini kontrol eder; yoksa UnauthorizedAccessException
// (handler calismaz). REST ve MCP ikisi de bus.InvokeAsync ile ayni handler'a ugradigi icin
// yetki TEK NOKTADA kontrol edilir. ScopeAuthorizationMiddleware'in rol ikizi.
public static class RoleAuthorizationMiddleware
{
    public static void Before(Envelope envelope, IHttpContextAccessor http)
    {
        var role = envelope.Message?.GetType()
            .GetCustomAttribute<RequiredRoleAttribute>()?.Role;
        if (role is null)
            return;

        // MapInboundClaims=false oldugu icin "role" claim'i ham; HasClaim dogrudan calisir.
        if (http.HttpContext?.User.HasClaim("role", role) != true)
            throw new UnauthorizedAccessException($"Required role missing: {role}");
    }
}
```

- [ ] **Step 4: `src/Identity.Server/Roles.cs`'e çapraz-referans yorumu ekle**

Mevcut:

```csharp
namespace Identity.Server;

// Uygulama geneli rol adlari. Magic string yerine tek kaynak.
public static class Roles
```

Şununla değiştir:

```csharp
namespace Identity.Server;

// Uygulama geneli rol adlari. Magic string yerine tek kaynak.
// NOT: Common.Utils.Constants.Roles ile ayni degerleri tasir (servisler onu kullanir);
// ikisi senkron kalmali. Identity.Server Common'i referans etmedigi icin ayri durur.
public static class Roles
```

- [ ] **Step 5: Common'ı derle**

Run: `dotnet build src/Common/Common.csproj`
Expected: BUILD SUCCEEDED, 0 error.

- [ ] **Step 6: Commit**

```bash
git add src/Common/Utils/Constants/Roles.cs src/Common/Utils/Authorization/RequiredRoleAttribute.cs src/Common/Utils/Authorization/RoleAuthorizationMiddleware.cs src/Identity.Server/Roles.cs
git commit -m "feat(common): add Roles constant + RequiredRole attribute and middleware"
```

---

### Task 2: catalog — write'lara Admin rolü

**Files:**
- Modify: `src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/CreateProduct.cs`
- Modify: `src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/UpdateProduct.cs`
- Modify: `src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/DeleteProduct.cs`
- Modify: `src/services/catalog/Catalog.Api/Program.cs`

**Interfaces:**
- Consumes: `RequiredRoleAttribute`, `Roles.Admin`, `RoleAuthorizationMiddleware` (Task 1). Bu üç catalog dosyası zaten `using Common.Utils.Authorization;` ve `using Common.Utils.Constants;` içerir; ek using gerekmez.

- [ ] **Step 1: `CreateProduct.cs`'e rol attribute'u ekle**

Mevcut:

```csharp
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record CreateProductCommand(
```

Şununla değiştir:

```csharp
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    [RequiredRole(Roles.Admin)]
    public record CreateProductCommand(
```

- [ ] **Step 2: `UpdateProduct.cs`'e rol attribute'u ekle**

Mevcut:

```csharp
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record UpdateProductCommand(
```

Şununla değiştir:

```csharp
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    [RequiredRole(Roles.Admin)]
    public record UpdateProductCommand(
```

- [ ] **Step 3: `DeleteProduct.cs`'e rol attribute'u ekle**

Mevcut:

```csharp
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record DeleteProductCommand(Guid Id);
```

Şununla değiştir:

```csharp
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    [RequiredRole(Roles.Admin)]
    public record DeleteProductCommand(Guid Id);
```

- [ ] **Step 4: catalog `Program.cs`'te role middleware'i weave et**

Mevcut (scope weave):

```csharp
    opts.Policies.AddMiddleware(
        typeof(ScopeAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiredScopeAttribute>() is not null);
```

Bu bloğun **hemen altına** ekle:

```csharp
    // Rol yetkisi: middleware SADECE [RequiredRole] tasiyan komut/sorgulara weave edilir.
    opts.Policies.AddMiddleware(
        typeof(RoleAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiredRoleAttribute>() is not null);
```

(catalog Program.cs zaten `using Common.Utils.Authorization;` içerir; `RoleAuthorizationMiddleware`/`RequiredRoleAttribute` çözülür.)

- [ ] **Step 5: catalog'ı derle**

Run: `dotnet build src/services/catalog/Catalog.Api/Catalog.Api.csproj`
Expected: BUILD SUCCEEDED, 0 error.

- [ ] **Step 6: Commit**

```bash
git add src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/CreateProduct.cs src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/UpdateProduct.cs src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/DeleteProduct.cs src/services/catalog/Catalog.Api/Program.cs
git commit -m "feat(catalog): require Admin role for product write operations"
```

---

### Task 3: discount — write'a Admin rolü + wiring

**Files:**
- Modify: `src/services/discount/Discount.Api/Domains/Discounts/Features/Commands/CreateDiscount.cs`
- Modify: `src/services/discount/Discount.Api/Program.cs`

**Interfaces:**
- Consumes: `RequiredRoleAttribute`, `Roles.Admin`, `RoleAuthorizationMiddleware` (Task 1). discount GlobalUsings'te `Common.Utils.Constants` var (`Roles` çözülür) ama `Common.Utils.Authorization` YOK → dosyalara eklenir.

- [ ] **Step 1: `CreateDiscount.cs`'e using + rol attribute'u ekle**

Dosyanın en başı şu an:

```csharp

namespace Discount.Api.Domains.Discounts.Features.Commands;

public static class CreateDiscount
{
    public record CreateDiscountCommand(Guid UserId, string Code, decimal Rate);
```

Şununla değiştir:

```csharp
using Common.Utils.Authorization;
using Common.Utils.Constants;

namespace Discount.Api.Domains.Discounts.Features.Commands;

public static class CreateDiscount
{
    [RequiredRole(Roles.Admin)]
    public record CreateDiscountCommand(Guid UserId, string Code, decimal Rate);
```

- [ ] **Step 2: discount `Program.cs`'e using ekle**

Mevcut ilk satırlar:

```csharp

using Shared.Utils.Constants;
```

Şununla değiştir:

```csharp

using Common.Utils.Authorization;
using Shared.Utils.Constants;
```

- [ ] **Step 3: discount `Program.cs`'te HttpContextAccessor + role weave ekle**

Mevcut Wolverine bloğunun sonu:

```csharp
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});
```

Şununla değiştir:

```csharp
    opts.Policies.UseDurableLocalQueues();
    // Rol yetkisi: middleware SADECE [RequiredRole] tasiyan komut/sorgulara weave edilir.
    opts.Policies.AddMiddleware(
        typeof(RoleAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiredRoleAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});
```

Ardından, `builder.Services.AddGlobalExceptionHandler();` satırının **hemen altına** ekle:

```csharp
// RoleAuthorizationMiddleware HttpContext'e erisir (token'daki role claim'i).
builder.Services.AddHttpContextAccessor();
```

- [ ] **Step 4: discount'ı derle**

Run: `dotnet build src/services/discount/Discount.Api/Discount.Api.csproj`
Expected: BUILD SUCCEEDED, 0 error.

- [ ] **Step 5: Commit**

```bash
git add src/services/discount/Discount.Api/Domains/Discounts/Features/Commands/CreateDiscount.cs src/services/discount/Discount.Api/Program.cs
git commit -m "feat(discount): require Admin role for discount creation"
```

---

### Task 4: stock — write'lara Admin rolü + wiring

**Files:**
- Modify: `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/IncreaseStock.cs`
- Modify: `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/DecreaseStock.cs`
- Modify: `src/services/stock/Stock.Api/Program.cs`

**Interfaces:**
- Consumes: `RequiredRoleAttribute`, `Roles.Admin`, `RoleAuthorizationMiddleware` (Task 1). stock GlobalUsings'te `Common.Utils.Constants` var; `Common.Utils.Authorization` YOK → dosyalara eklenir.

- [ ] **Step 1: `IncreaseStock.cs`'e using + rol attribute'u ekle**

Dosyanın en başı şu an:

```csharp
namespace Stock.Api.Domains.Stocks.Features.Commands;

public static class IncreaseStock
{
    public record IncreaseStockCommand(Guid ProductId, int Amount);
```

Şununla değiştir:

```csharp
using Common.Utils.Authorization;
using Common.Utils.Constants;

namespace Stock.Api.Domains.Stocks.Features.Commands;

public static class IncreaseStock
{
    [RequiredRole(Roles.Admin)]
    public record IncreaseStockCommand(Guid ProductId, int Amount);
```

- [ ] **Step 2: `DecreaseStock.cs`'e using + rol attribute'u ekle**

Dosyanın en başı şu an:

```csharp
namespace Stock.Api.Domains.Stocks.Features.Commands;

public static class DecreaseStock
{
    public record DecreaseStockCommand(Guid ProductId, int Amount);
```

Şununla değiştir:

```csharp
using Common.Utils.Authorization;
using Common.Utils.Constants;

namespace Stock.Api.Domains.Stocks.Features.Commands;

public static class DecreaseStock
{
    [RequiredRole(Roles.Admin)]
    public record DecreaseStockCommand(Guid ProductId, int Amount);
```

- [ ] **Step 3: stock `Program.cs`'e using ekle**

Mevcut ilk satırlar:

```csharp

using Shared.Utils.Constants;
```

Şununla değiştir:

```csharp

using Common.Utils.Authorization;
using Shared.Utils.Constants;
```

- [ ] **Step 4: stock `Program.cs`'te HttpContextAccessor + role weave ekle**

Mevcut Wolverine bloğunun sonu:

```csharp
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});
```

Şununla değiştir:

```csharp
    opts.Policies.UseDurableLocalQueues();
    // Rol yetkisi: middleware SADECE [RequiredRole] tasiyan komut/sorgulara weave edilir.
    opts.Policies.AddMiddleware(
        typeof(RoleAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiredRoleAttribute>() is not null);
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
});
```

Ardından, `builder.Services.AddGlobalExceptionHandler();` satırının **hemen altına** ekle:

```csharp
// RoleAuthorizationMiddleware HttpContext'e erisir (token'daki role claim'i).
builder.Services.AddHttpContextAccessor();
```

- [ ] **Step 5: stock'u derle**

Run: `dotnet build src/services/stock/Stock.Api/Stock.Api.csproj`
Expected: BUILD SUCCEEDED, 0 error.

- [ ] **Step 6: Commit**

```bash
git add src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/IncreaseStock.cs src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/DecreaseStock.cs src/services/stock/Stock.Api/Program.cs
git commit -m "feat(stock): require Admin role for stock increase/decrease"
```

---

### Task 5: Tam derleme + manuel doğrulama

**Files:** (yok — doğrulama)

- [ ] **Step 1: Solution'ı derle**

Run: `dotnet build ECommerceWithAgentFramework.slnx`
Expected: BUILD SUCCEEDED, 0 error.

- [ ] **Step 2: (Manuel) Aspire ile doğrula**

Run: `dotnet run --project src/AppHost`

Beklenenler:
1. **Admin** (seed: `admin@ecommerce.local`) ile login → catalog product create/update/delete, discount create, stock increase/decrease (REST) → **başarılı (200)**.
2. **Customer** (self-register) ile aynı write'lar → **403**.
3. **Customer** ile basket'e ekle / order / payment → **hâlâ başarılı** (etkilenmedi).
4. catalog `delete_product` MCP tool'u Customer token'ıyla → **403** (handler-level koruma MCP'de de).

Not: Docker + Aspire gerektiren manuel adım; otomatik test yok.

---

## Self-Review Notları

- **Spec kapsamı:** Roles sabiti (T1) ✓, RequiredRole (T1) ✓, RoleAuthorizationMiddleware (T1) ✓, 6 write komutuna attribute (catalog T2 / discount T3 / stock T4) ✓, servis wiring (T2/T3/T4) ✓, scope korunuyor (attribute'lar eklenir, silinmez) ✓, doğrulama (T5) ✓.
- **Placeholder yok.**
- **Tip/isim tutarlılığı:** `RequiredRole`/`RoleAuthorizationMiddleware`/`Roles.Admin` her yerde aynı; weave predicate `RequiredRoleAttribute` her serviste birebir.
- **using ihtiyacı:** discount/stock komut ve Program dosyalarına `Common.Utils.Authorization` eklenir (GlobalUsings'te yok); catalog'da zaten var.