# Rol Geliştirmelerinin Kaldırılması — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin/Customer rol kavramını (kimlik tohumlama + admin-only yazma zorlaması) projeden tümüyle kaldırmak; yazma uçları yalnızca `[RequiredScope]` ile korunan rol-öncesi davranışa döner.

**Architecture:** Üç adımda, her adımda derleme yeşil kalacak şekilde: (1) Identity.Server tarafındaki rol/admin tohumlamayı sil, (2) servislerdeki `[RequiredRole]` attribute'larını ve rol middleware kayıtlarını kaldır, (3) artık kullanılmayan Common rol tiplerini sil. Kullanımlar tiplerden ÖNCE kaldırıldığı için ara adımlarda dangling referans oluşmaz.

**Tech Stack:** .NET 10, Aspire, Wolverine, Duende IdentityServer + ASP.NET Identity, Marten/Postgres.

## Global Constraints

- Build komutu tek doğrulama noktasıdır (test projesi yok): `dotnet build ECommerceWithAgentFramework.slnx`.
- Kod yorumları Türkçe yazılır (mevcut konvansiyon).
- `NuGet` sürümleri merkezi; csproj'a `Version=` eklenmez (bu planda paket değişikliği yok).
- **DB / migration'lara dokunulmaz** — `AspNetRoles`/`AspNetUserRoles` tabloları bırakılır (boş kalır).
- `[RequiredScope(...)]` attribute'ları ve `ScopeAuthorizationMiddleware` kayıtları KALIR — scope yetkisi rol işinden bağımsızdır.
- Working tree'de rol ile ilgisiz refactor değişiklikleri var; onlara dokunulmaz. Her commit'te yalnızca ilgili dosyalar stage edilir.
- Not: `src/Identity.Server/Roles.cs` kullanıcı tarafından zaten silindi (git'te staged `D`). Bu yüzden şu an derleme kırık; Task 1 bunu düzeltir.

---

### Task 1: Identity.Server rol/admin tohumlamayı kaldır

**Files:**
- Delete: `src/Identity.Server/Data/IdentitySeed.cs`
- Modify: `src/Identity.Server/Program.cs`
- Modify: `src/Identity.Server/Pages/Account/Create/Index.cshtml.cs`
- Modify: `src/Identity.Server/appsettings.Development.json`
- (Zaten silinmiş) `src/Identity.Server/Roles.cs`

**Interfaces:**
- Consumes: yok.
- Produces: `IdentitySeed.SeedAsync` ve `Roles` tipleri artık yok; sonraki task'lar bunlara referans vermemeli.

- [ ] **Step 1: `IdentitySeed.cs` dosyasını sil**

```bash
git rm src/Identity.Server/Data/IdentitySeed.cs
```

- [ ] **Step 2: `Program.cs`'ten seed çağrısını ve gereksiz using'i kaldır**

`src/Identity.Server/Program.cs` içinde şu satırı sil (dosyanın başındaki using'ler arasında):

```csharp
using Identity.Server.Data;
```

Ve migration bloğundan sonraki şu iki satırı (yorum + çağrı) sil:

```csharp
// Migration'lardan SONRA: tablolar hazir olunca rolleri ve admin kullanicisini seed et.
await app.SeedAsync();
```

Not: `AddIdentity<ApplicationUser, IdentityRole>()` satırı DEĞİŞTİRİLMEZ (standart Identity iskeleti; tablolar bırakılıyor).

- [ ] **Step 3: Kayıt sayfasından Customer rol atamasını kaldır**

`src/Identity.Server/Pages/Account/Create/Index.cshtml.cs` içinde şu iki satırı (yorum + çağrı) sil:

```csharp
            // Yeni kayit olan her kullanici varsayilan olarak Customer rolune girer.
            await _userManager.AddToRoleAsync(user, Roles.Customer);
```

- [ ] **Step 4: `appsettings.Development.json`'dan SeedAdmin bloğunu kaldır**

`src/Identity.Server/appsettings.Development.json` şu hale gelmeli:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 5: Identity.Server derlensin**

Run: `dotnet build src/Identity.Server/Identity.Server.csproj`
Expected: Build succeeded (0 error). `Roles`/`IdentitySeed` referansı kalmadığı için hata yok.

- [ ] **Step 6: Commit**

```bash
git add src/Identity.Server/Roles.cs src/Identity.Server/Data/IdentitySeed.cs \
        src/Identity.Server/Program.cs \
        "src/Identity.Server/Pages/Account/Create/Index.cshtml.cs" \
        src/Identity.Server/appsettings.Development.json
git commit -m "refactor(identity): remove role and admin-user seeding"
```

---

### Task 2: Servislerden `[RequiredRole]` ve rol middleware'ini kaldır

**Files:**
- Modify: `src/services/catalog/Catalog.Api/Program.cs`
- Modify: `src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/CreateProduct.cs`
- Modify: `src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/UpdateProduct.cs`
- Modify: `src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/DeleteProduct.cs`
- Modify: `src/services/discount/Discount.Api/Program.cs`
- Modify: `src/services/discount/Discount.Api/Domains/Discounts/Features/Commands/CreateDiscount.cs`
- Modify: `src/services/stock/Stock.Api/Program.cs`
- Modify: `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/IncreaseStock.cs`
- Modify: `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/DecreaseStock.cs`

**Interfaces:**
- Consumes: yok (Common rol tipleri hâlâ mevcut; bu task yalnızca kullanımlarını siler).
- Produces: `RequiredRoleAttribute` / `RoleAuthorizationMiddleware` / `Common.Utils.Constants.Roles` artık hiçbir yerde kullanılmaz → Task 3'te silinebilir.

- [ ] **Step 1: 6 komut dosyasından `[RequiredRole(Roles.Admin)]` attribute satırını kaldır**

Aşağıdaki her dosyada, ilgili record'un üstündeki tek satırlık `[RequiredRole(Roles.Admin)]` attribute'unu sil (`[RequiredScope(...)]` satırı KALIR):

- `CreateProduct.cs`, `UpdateProduct.cs`, `DeleteProduct.cs` (catalog)
- `CreateDiscount.cs` (discount)
- `IncreaseStock.cs`, `DecreaseStock.cs` (stock)

Örnek — `CreateProduct.cs` şu hale gelir:

```csharp
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
    public record CreateProductCommand(
```

(Kullanılmayan hale gelen `using Common.Utils.Authorization;` / `using Common.Utils.Constants;` importları derlemeyi kırmaz; derleme sonrası uyarı görünürse kaldırılabilir, zorunlu değil.)

- [ ] **Step 2: catalog `Program.cs`'ten rol middleware kaydını kaldır**

`src/services/catalog/Catalog.Api/Program.cs` içinde şu bloğu (yorum + kayıt) sil:

```csharp
    // Rol yetkisi: middleware SADECE [RequiredRole] tasiyan komut/sorgulara weave edilir.
    opts.Policies.AddMiddleware(
        typeof(RoleAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiredRoleAttribute>() is not null);
```

`ScopeAuthorizationMiddleware` kaydı ve `builder.Services.AddHttpContextAccessor();` (MCP/scope için gerekli) DEĞİŞTİRİLMEZ.

- [ ] **Step 3: stock `Program.cs`'ten rol middleware + HttpContextAccessor'ı kaldır**

`src/services/stock/Stock.Api/Program.cs` — Wolverine bloğundaki şu satırları sil:

```csharp
    // Rol yetkisi: middleware SADECE [RequiredRole] tasiyan komut/sorgulara weave edilir.
    opts.Policies.AddMiddleware(
        typeof(RoleAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiredRoleAttribute>() is not null);
```

Ve servis kayıtlarından şu iki satırı sil (bu servis `ScopeAuthorizationMiddleware` kaydetmiyor; `AddHttpContextAccessor` yalnızca rol middleware'i için eklenmişti):

```csharp
// RoleAuthorizationMiddleware HttpContext'e erisir (token'daki role claim'i).
builder.Services.AddHttpContextAccessor();
```

- [ ] **Step 4: discount `Program.cs`'ten rol middleware + HttpContextAccessor'ı kaldır**

`src/services/discount/Discount.Api/Program.cs` — Wolverine bloğundaki şu satırları sil:

```csharp
    // Rol yetkisi: middleware SADECE [RequiredRole] tasiyan komut/sorgulara weave edilir.
    opts.Policies.AddMiddleware(
        typeof(RoleAuthorizationMiddleware),
        chain => chain.MessageType.GetCustomAttribute<RequiredRoleAttribute>() is not null);
```

Ve servis kayıtlarından şu iki satırı sil (gerekçe stock ile aynı):

```csharp
// RoleAuthorizationMiddleware HttpContext'e erisir (token'daki role claim'i).
builder.Services.AddHttpContextAccessor();
```

- [ ] **Step 5: Üç servis de derlensin**

Run: `dotnet build src/services/catalog/Catalog.Api/Catalog.Api.csproj src/services/discount/Discount.Api/Discount.Api.csproj src/services/stock/Stock.Api/Stock.Api.csproj`
Expected: Build succeeded (0 error).

- [ ] **Step 6: Commit**

```bash
git add src/services/catalog/Catalog.Api/Program.cs \
        src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/CreateProduct.cs \
        src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/UpdateProduct.cs \
        src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/DeleteProduct.cs \
        src/services/discount/Discount.Api/Program.cs \
        src/services/discount/Discount.Api/Domains/Discounts/Features/Commands/CreateDiscount.cs \
        src/services/stock/Stock.Api/Program.cs \
        src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/IncreaseStock.cs \
        src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/DecreaseStock.cs
git commit -m "refactor(services): drop admin-role enforcement on write commands"
```

---

### Task 3: Kullanılmayan Common rol tiplerini sil

**Files:**
- Delete: `src/Common/Utils/Constants/Roles.cs`
- Delete: `src/Common/Utils/Authorization/RequiredRoleAttribute.cs`
- Delete: `src/Common/Utils/Authorization/RoleAuthorizationMiddleware.cs`

**Interfaces:**
- Consumes: yok — bu tipler Task 1 ve 2 sonrası hiçbir yerde kullanılmıyor.
- Produces: yok.

- [ ] **Step 1: Kalan referans olmadığını doğrula**

Run: `grep -rn "RequiredRole\|RoleAuthorizationMiddleware\|Common.Utils.Constants.Roles\|Roles.Admin\|Roles.Customer" src --include=*.cs`
Expected: Yalnızca silinecek üç dosyanın kendi içindeki tanımlar çıkar (başka kullanım YOK).

- [ ] **Step 2: Üç dosyayı sil**

```bash
git rm src/Common/Utils/Constants/Roles.cs \
       src/Common/Utils/Authorization/RequiredRoleAttribute.cs \
       src/Common/Utils/Authorization/RoleAuthorizationMiddleware.cs
```

- [ ] **Step 3: Tüm çözüm derlensin**

Run: `dotnet build ECommerceWithAgentFramework.slnx`
Expected: Build succeeded (0 error).

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor(common): remove unused role authorization types"
```

---

### Task 4: Belleği güncelle

**Files:**
- Modify: `/Users/macbook/.claude/projects/-Users-macbook-Desktop-ECommerceWithAgentFramework/memory/roles-status.md`

- [ ] **Step 1: `roles-status.md`'yi güncelle**

Mevcut "phase 1+2 merged" içeriğini, bu geliştirmelerin 2026-07-08'de geri alındığını (rol kavramı koddan tamamen kaldırıldı; yazma uçları yalnızca `[RequiredScope]` ile korunuyor; `AspNetRoles` tabloları DB'de boş bırakıldı; phase 3 zaten deferred'dı) yansıtacak şekilde düzenle. `MEMORY.md`'deki tek satırlık özeti de güncelle.

Not: Bu adım kod deposuna commit gerektirmez (bellek dizini repo dışıdır).

---

## Self-Review Notu

- Spec kapsamı → görevler: kimlik tohumlama (Task 1), servis yazma zorlaması (Task 2), Common tipleri (Task 3), bellek (Task 4). Tüm silinecek/düzenlenecek dosyalar karşılandı.
- Sıralama: kullanımlar (Task 1-2) tiplerden (Task 3) önce kaldırıldığı için her ara adımda derleme yeşil.
- Catalog vs stock/discount ayrımı: `AddHttpContextAccessor` yalnızca stock/discount'ta rol için eklenmişti; catalog'da MCP/scope için gerekli, korunuyor.