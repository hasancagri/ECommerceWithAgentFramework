# Admin/Customer Rolleri + Admin Seed — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Identity.Server'da `Admin`/`Customer` rollerini ve config'den bir admin kullanıcıyı seed etmek; self-register olan kullanıcılara `Customer` rolü atamak.

**Architecture:** Açılışta (migration'lardan sonra) çalışan idempotent bir seeder rolleri ve admin kullanıcıyı oluşturur. Rol claim'i mevcut IdentityServer config'i (roles resource + AlwaysIncludeUserClaimsInIdToken) sayesinde otomatik token'a akar; config'e dokunulmaz. Create sayfası yeni kullanıcıya Customer rolü ekler.

**Tech Stack:** .NET 10, ASP.NET Core Identity (`RoleManager`/`UserManager`), Duende IdentityServer, Npgsql/EF Core.

## Global Constraints

- Yalnızca `src/Identity.Server` değişir; hiçbir servis veya ChatAgent'a dokunulmaz.
- IdentityServer `Config.cs`'e dokunulmaz (rol claim akışı zaten kurulu).
- Seeder idempotent olmalı ve hata durumunda uygulamayı bloklamamalı (exception fırlatmaz, `ILogger` ile uyarır).
- Rol adları magic string değil, `Roles` sabitlerinden gelir.
- Admin bilgileri config'den: `SeedAdmin:Email`, `SeedAdmin:Password`. Dev default `appsettings.Development.json`'da; prod'da user-secrets/env ile ezilir. `appsettings.json`'a eklenmez.
- Kod yorumları Türkçe.
- Test altyapısı yok → doğrulama `dotnet build` + manuel Aspire çalıştırması.

---

### Task 1: Roller + admin kullanıcı seed'i

**Files:**
- Create: `src/Identity.Server/Roles.cs`
- Create: `src/Identity.Server/Data/IdentitySeed.cs`
- Modify: `src/Identity.Server/Program.cs` (migration `using` bloğunun hemen ardı)
- Modify: `src/Identity.Server/appsettings.Development.json`

**Interfaces:**
- Produces:
  - `Identity.Server.Roles.Admin` (`"Admin"`), `Identity.Server.Roles.Customer` (`"Customer"`)
  - `Task IdentitySeed.SeedAsync(this WebApplication app)` (namespace `Identity.Server`)

- [ ] **Step 1: `Roles.cs` oluştur**

```csharp
namespace Identity.Server;

// Uygulama geneli rol adlari. Magic string yerine tek kaynak.
public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}
```

- [ ] **Step 2: `Data/IdentitySeed.cs` oluştur**

```csharp
using System.Security.Claims;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Identity;

namespace Identity.Server;

// Acilista rolleri ve seed admin kullanicisini olusturur. Idempotent: her acilista
// guvenle calisir, var olanlara dokunmaz. Hata uygulamayi bloklamaz; ILogger'a yazilir.
public static class IdentitySeed
{
    public static async Task SeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeed");

        // 1) Roller (Admin, Customer) yoksa olustur.
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Admin, Roles.Customer })
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
                logger.LogWarning("Rol '{Role}' olusturulamadi: {Errors}",
                    role, string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        // 2) Admin kullanici (config'den). Bilgi yoksa atla.
        var email = app.Configuration["SeedAdmin:Email"];
        var password = app.Configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("SeedAdmin:Email/Password bos; admin kullanici seed'i atlandi.");
            return;
        }

        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByEmailAsync(email) is not null)
            return; // zaten var, idempotent

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            logger.LogWarning("Admin kullanici olusturulamadi: {Errors}",
                string.Join("; ", created.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, Roles.Admin);
        await userManager.AddClaimsAsync(user,
        [
            new Claim(JwtClaimTypes.Name, email),
            new Claim(JwtClaimTypes.Email, email),
        ]);
        logger.LogInformation("Seed admin kullanicisi olusturuldu: {Email}", email);
    }
}
```

- [ ] **Step 3: `Program.cs`'te seeder'ı çağır**

Mevcut migration bloğu:

```csharp
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();
}
```

Bu bloğun **hemen altına** ekle:

```csharp
// Migration'lardan SONRA: tablolar hazir olunca rolleri ve admin kullanicisini seed et.
await app.SeedAsync();
```

(`Program.cs` top-level statements; `await` kullanılabilir. `using Identity.Server;` zaten mevcut, `SeedAsync` çözülür.)

- [ ] **Step 4: `appsettings.Development.json`'a admin bilgilerini ekle**

Dosyanın tamamını şu içerikle değiştir:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "SeedAdmin": {
    "Email": "admin@ecommerce.local",
    "Password": "Admin!123"
  }
}
```

- [ ] **Step 5: Derle**

Run: `dotnet build src/Identity.Server/Identity.Server.csproj`
Expected: BUILD SUCCEEDED, 0 error.

- [ ] **Step 6: Commit**

```bash
git add src/Identity.Server/Roles.cs src/Identity.Server/Data/IdentitySeed.cs src/Identity.Server/Program.cs src/Identity.Server/appsettings.Development.json
git commit -m "feat(identity): seed Admin/Customer roles and admin user on startup"
```

---

### Task 2: Self-register → Customer rolü

**Files:**
- Modify: `src/Identity.Server/Pages/Account/Create/Index.cshtml.cs`

**Interfaces:**
- Consumes: `Roles.Customer` (Task 1).

- [ ] **Step 1: Rol atamasını ekle**

`OnPost` içinde, mevcut claim ekleme bloğu:

```csharp
            if (claims.Count > 0)
                await _userManager.AddClaimsAsync(user, claims);
```

Bu bloğun **hemen altına** ekle:

```csharp
            // Yeni kayit olan her kullanici varsayilan olarak Customer rolune girer.
            await _userManager.AddToRoleAsync(user, Roles.Customer);
```

(`Roles`, `Identity.Server` namespace'inde; sayfa `Identity.Server.Pages.Create` altında olduğu için ek `using` gerekmez.)

- [ ] **Step 2: Derle**

Run: `dotnet build src/Identity.Server/Identity.Server.csproj`
Expected: BUILD SUCCEEDED, 0 error.

- [ ] **Step 3: Commit**

```bash
git add src/Identity.Server/Pages/Account/Create/Index.cshtml.cs
git commit -m "feat(identity): assign Customer role to self-registered users"
```

---

### Task 3: Tam derleme + manuel doğrulama

**Files:** (yok — doğrulama)

- [ ] **Step 1: Solution'ı derle**

Run: `dotnet build ECommerceWithAgentFramework.slnx`
Expected: BUILD SUCCEEDED, 0 error.

- [ ] **Step 2: (Manuel) Aspire ile çalıştır ve doğrula**

Run: `dotnet run --project src/AppHost`

Beklenen doğrulamalar:
1. Identity.Server açılış log'unda `Seed admin kullanicisi olusturuldu: admin@ecommerce.local` görülür (ilk açılışta).
2. WebApp'ten `admin@ecommerce.local` / `Admin!123` ile login → kullanıcı principal'ında / token'da `role=Admin` claim'i bulunur.
3. Create sayfasından yeni bir kullanıcı kaydı → o kullanıcıda `role=Customer` bulunur.
4. `dotnet run` tekrar → seeder hata vermez, duplicate admin/rol oluşmaz (idempotent; log'da "atlandi" veya sessiz geçiş).

Not: Docker + Aspire gerektiren manuel adım; otomatik test yok.

---

## Self-Review Notları

- **Spec kapsamı:** Roller (Task 1 Step 1-2) ✓, admin seed (Task 1) ✓, Program wiring (Task 1 Step 3) ✓, config (Task 1 Step 4) ✓, self-register Customer (Task 2) ✓, idempotency + hata bloklamama (IdentitySeed kodu) ✓, doğrulama (Task 3) ✓.
- **Placeholder yok.**
- **Tip/isim tutarlılığı:** `Roles.Admin`/`Roles.Customer`, `SeedAsync`, `SeedAdmin:Email`/`SeedAdmin:Password` her yerde aynı.
- **Config'e dokunulmuyor:** rol claim akışı mevcut `Config.cs` ile sağlanıyor (spec ile tutarlı).