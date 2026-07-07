# Scalar API Dokümantasyonu — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 7 mikroservise yerleşik OpenAPI + Scalar UI eklemek; kurulum ServiceDefaults'ta ortak iki extension'da toplanır.

**Architecture:** `ServiceDefaults` içine `AddOpenApiDocumentation` (builder) ve `MapScalarDocumentation` (app, yalnız Development) extension'ları eklenir. İki NuGet paketi doğrudan `ServiceDefaults.csproj`'a konur; 7 servis onu referans ettiği için paketler transitive gelir. Her servis `Program.cs`'e iki satır ekler.

**Tech Stack:** .NET 10, `Microsoft.AspNetCore.OpenApi` 10.0.9, `Scalar.AspNetCore` 2.16.10, Wolverine minimal API, Asp.Versioning.

## Global Constraints

- Merkezî paket yönetimi: `Directory.Packages.props`'a `PackageVersion`; csproj'larda `Version=` YOK.
- Kod yorumları Türkçe.
- Scalar/OpenAPI endpoint'leri yalnız `app.Environment.IsDevelopment()` iken map'lenir.
- Auth/Bearer güvenlik şeması EKLENMEZ (sade dokümantasyon).
- Namespace `Microsoft.Extensions.Hosting` (mevcut `Extensions.cs` ile aynı).
- Kapsam: catalog, basket, order, discount, payment, stock, file. Gateway ve Identity.Server hariç.

---

### Task 1: Paketleri ekle

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/ServiceDefaults/ServiceDefaults.csproj`

**Interfaces:**
- Produces: `Microsoft.AspNetCore.OpenApi` ve `Scalar.AspNetCore` paketleri, ServiceDefaults'u referans eden tüm servislerde transitive kullanılabilir.

- [ ] **Step 1: `Directory.Packages.props`'a iki `PackageVersion` ekle**

`<ItemGroup>` içine, alfabetik sıraya uygun konuma:

```xml
        <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.9" />
        <PackageVersion Include="Scalar.AspNetCore" Version="2.16.10" />
```

(`Microsoft.AspNetCore.OpenApi`, mevcut `Microsoft.AspNetCore.Mvc.Abstractions` satırından sonra; `Scalar.AspNetCore`, `PagedList.Core` ile `Refit...` arasına — sıralama şart değil, derleme etkilenmez.)

- [ ] **Step 2: `src/ServiceDefaults/ServiceDefaults.csproj`'a iki `PackageReference` ekle**

Mevcut `<ItemGroup>` içine (FrameworkReference'ın olduğu grup):

```xml
        <PackageReference Include="Microsoft.AspNetCore.OpenApi"/>
        <PackageReference Include="Scalar.AspNetCore"/>
```

- [ ] **Step 3: Restore/derleme ile doğrula**

Run: `dotnet build src/ServiceDefaults/ServiceDefaults.csproj`
Expected: BUILD SUCCEEDED (paketler restore edildi).

- [ ] **Step 4: Commit**

```bash
git add Directory.Packages.props src/ServiceDefaults/ServiceDefaults.csproj
git commit -m "build: add OpenApi and Scalar packages to ServiceDefaults"
```

---

### Task 2: OpenApiExtensions.cs oluştur

**Files:**
- Create: `src/ServiceDefaults/OpenApiExtensions.cs`

**Interfaces:**
- Consumes: Task 1 paketleri (`AddOpenApi`, `MapOpenApi`, `MapScalarApiReference`).
- Produces:
  - `IHostApplicationBuilder AddOpenApiDocumentation(this IHostApplicationBuilder builder)`
  - `WebApplication MapScalarDocumentation(this WebApplication app)`

- [ ] **Step 1: Dosyayı yaz**

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

namespace Microsoft.Extensions.Hosting;

// Servislere yerlesik OpenAPI belgesi ve Scalar arayuzu ekler.
// Paketler ServiceDefaults.csproj'da; ServiceDefaults'u referans eden her servis kullanabilir.
public static class OpenApiExtensions
{
    // OpenAPI belge uretimini kaydeder. Auth guvenlik semasi eklenmez (sade dokumantasyon).
    public static IHostApplicationBuilder AddOpenApiDocumentation(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        return builder;
    }

    // OpenAPI JSON (/openapi/v1.json) ve Scalar UI (/scalar/v1) yalnizca Development'ta acilir.
    // Production'da hicbir dokumantasyon endpoint'i map'lenmez.
    public static WebApplication MapScalarDocumentation(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        return app;
    }
}
```

- [ ] **Step 2: Derle**

Run: `dotnet build src/ServiceDefaults/ServiceDefaults.csproj`
Expected: BUILD SUCCEEDED.

- [ ] **Step 3: Commit**

```bash
git add src/ServiceDefaults/OpenApiExtensions.cs
git commit -m "feat: add OpenApi + Scalar extensions to ServiceDefaults"
```

---

### Task 3: 7 serviste Program.cs'e bağla

**Files:**
- Modify: `src/services/catalog/Catalog.Api/Program.cs`
- Modify: `src/services/basket/Basket.Api/Program.cs`
- Modify: `src/services/order/Order.Api/Program.cs`
- Modify: `src/services/discount/Discount.Api/Program.cs`
- Modify: `src/services/payment/Payment.Api/Program.cs`
- Modify: `src/services/stock/Stock.Api/Program.cs`
- Modify: `src/services/file/File.Api/Program.cs`

**Interfaces:**
- Consumes: `AddOpenApiDocumentation` ve `MapScalarDocumentation` (Task 2). Extension'lar `Microsoft.Extensions.Hosting` namespace'inde; `WebApplication.CreateBuilder` zaten bu namespace'i getirir, ekstra using gerekmez.

- [ ] **Step 1: Her serviste builder kaydını ekle**

Her `Program.cs`'te `var builder = WebApplication.CreateBuilder(args);` satırının HEMEN ALTINA ekle:

```csharp
builder.AddOpenApiDocumentation();
```

- [ ] **Step 2: Her serviste app map'ini ekle**

Her `Program.cs`'te `var app = builder.Build();` satırının HEMEN ALTINA ekle:

```csharp
app.MapScalarDocumentation();
```

- [ ] **Step 3: 7 servisin tamamının eklendiğini doğrula**

Run: `grep -rl "AddOpenApiDocumentation" src/services --include=Program.cs | wc -l`
Expected: `7`

Run: `grep -rl "MapScalarDocumentation" src/services --include=Program.cs | wc -l`
Expected: `7`

- [ ] **Step 4: Commit**

```bash
git add src/services/*/*.Api/Program.cs
git commit -m "feat: wire Scalar API docs into all 7 microservices"
```

---

### Task 4: Tüm solution derle ve doğrula

**Files:** (yok — doğrulama task'ı)

- [ ] **Step 1: Solution'ı derle**

Run: `dotnet build ECommerceWithAgentFramework.slnx`
Expected: BUILD SUCCEEDED, 0 error.

- [ ] **Step 2: (Manuel) Aspire ile çalıştır ve UI'yi aç**

Run: `dotnet run --project src/AppHost`
Beklenen: Aspire dashboard açılır. Bir servisin (ör. catalog) endpoint'inden:
- `/scalar/v1` → Scalar arayüzü, Products endpoint'leri listeli.
- `/openapi/v1.json` → geçerli OpenAPI JSON.

Not: Bu adım manuel; Docker + Aspire gerektirir. Otomatik test yok (repoda test altyapısı yok).

---

## Self-Review Notları

- **Spec kapsamı:** Paketler (Task 1) ✓, extension'lar (Task 2) ✓, 7 servis entegrasyonu (Task 3) ✓, Development kapısı (Task 2 `MapScalarDocumentation`) ✓, Bearer yok (Task 2) ✓, doğrulama (Task 4) ✓.
- **Sürümlü route inceliği:** Spec'te not düşülmüş; kod değişikliği gerektirmez (yerleşik davranış).
- **Placeholder yok**, tip/isim tutarlı (`AddOpenApiDocumentation`, `MapScalarDocumentation` her yerde aynı).