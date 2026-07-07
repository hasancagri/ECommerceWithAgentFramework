# Admin/Customer Rolleri + Admin Kullanıcı Seed'i — Tasarım

**Tarih:** 2026-07-07
**Durum:** Onaylandı

## Amaç

Uygulamada `Admin` ve `Customer` rollerini tanımlamak, Admin rolüne sahip bir
kullanıcıyı seed etmek ve self-register olan kullanıcılara otomatik `Customer`
rolü vermek. Böylece bir kullanıcı Admin rolüyle giriş yapabilir ve `role` claim'i
token'a akar.

Bu, daha büyük "rol bazlı yetki" hedefinin **ilk fazıdır**. Kapsam dışı (sonraki
fazlar):

- **Backend enforcement** (rol → yetki; ör. yalnız Admin write yapabilsin).
- **ChatAgent MCP tool ayrımı** (role göre farklı tool seti — Singleton agent'ı
  request-aware yapmayı gerektirir, ertelenmiş "Option C" borcu).

Bu faz sadece kimlik + rol katmanını kurar; hiçbir servis veya ChatAgent değişmez.

## Mevcut durum (neden az iş)

`Identity.Server` altyapısı rolleri zaten destekliyor:

- `AddIdentity<ApplicationUser, IdentityRole>()` — rol yönetimi açık.
- `Config.cs`: `roles` IdentityResource `role` claim'ini taşıyor; tüm
  ApiResource'lar `UserClaims`'te `role` içeriyor; `ecommerce.bff` client
  `AlwaysIncludeUserClaimsInIdToken = true` ve `roles` scope'una sahip.

Sonuç: bir kullanıcı bir role atanınca `role` claim'i **otomatik** id_token ve
access token'a akar. IdentityServer config'e bu fazda dokunulmaz.

Eksik olan tek şey: rollerin ve bir admin kullanıcının **seed edilmesi**, bir de
self-register akışında rol atanması.

## Bileşenler

### 1. Rol adları sabiti

- Yeni dosya: `src/Identity.Server/Roles.cs`
- `public static class Roles { public const string Admin = "Admin"; public const string Customer = "Customer"; }`
- Magic string yok. İleride backend enforcement fazında `Shared`'a taşınabilir;
  şimdilik `Config.cs` gibi Identity.Server'a yerel (YAGNI).

### 2. Seeder

- Yeni dosya: `src/Identity.Server/Data/IdentitySeed.cs`
- İmza: `public static async Task SeedAsync(this WebApplication app)`
- Bir DI scope açar; `RoleManager<IdentityRole>` ve `UserManager<ApplicationUser>`
  alır.
- **Rol seed'i:** `Roles.Admin` ve `Roles.Customer` yoksa `CreateAsync` ile oluşturur
  (idempotent).
- **Admin kullanıcı seed'i:**
  - Config'den okur: `SeedAdmin:Email`, `SeedAdmin:Password`.
  - Email boşsa: uyarı log'la ve admin seed'i atla (roller yine oluşturulur).
  - `FindByEmailAsync` ile varsa dokunma (idempotent).
  - Yoksa: `ApplicationUser { UserName = email, Email = email, EmailConfirmed = true }`
    oluştur (`CreateAsync(user, password)`); başarısızsa hataları log'la ve durma
    (uygulama açılışını bloklamaz).
  - Başarılıysa `AddToRoleAsync(user, Roles.Admin)` ve Create sayfasındakiyle
    aynı `name`/`email` claim'lerini (`JwtClaimTypes`) ekle.

### 3. Program.cs entegrasyonu

- `src/Identity.Server/Program.cs`: mevcut `Database.Migrate()` bloğunun **hemen
  ardından** `await app.SeedAsync();` çağrısı. (Migration'lar seed'den önce
  bitmeli; roller/kullanıcılar için tablolar hazır olmalı.)
- `Main` zaten top-level statements; `await` kullanılabilir.

### 4. Self-register → Customer

- `src/Identity.Server/Pages/Account/Create/Index.cshtml.cs`, `OnPost`:
  `CreateAsync` başarılı olduktan **sonra**, mevcut claim ekleme bloğunun yanında
  `await _userManager.AddToRoleAsync(user, Roles.Customer);` eklenir.

### 5. Config

- `src/Identity.Server/appsettings.Development.json`: dev default
  ```json
  "SeedAdmin": { "Email": "admin@ecommerce.local", "Password": "Admin!123" }
  ```
- Gerçek ortamda user-secrets/env ile ezilir (`SeedAdmin:Password`).
- `appsettings.json`'a eklenmez (dev-only default; prod'da açıkça verilmeli).

## Hata yönetimi

- Seeder açılışta çalışır; hata uygulamayı bloklamamalı. Rol/kullanıcı oluşturma
  başarısızlıkları `ILogger` ile uyarı olarak log'lanır, exception fırlatılmaz
  (Postgres hazır ama örn. parola politikası tutmazsa açılış çökmesin).
- Idempotent: tekrar tekrar çalıştırma güvenli (her açılışta çalışır).

## Test / Doğrulama

Repoda test altyapısı yok. Manuel doğrulama:

1. `dotnet build ECommerceWithAgentFramework.slnx` — derleme geçmeli.
2. `dotnet run --project src/AppHost` — Aspire ile ayağa kaldır.
3. Seed'lenen admin (`admin@ecommerce.local` / `Admin!123`) ile WebApp üzerinden
   login → principal/token'da `role=Admin` claim'i gelmeli.
4. Create ile yeni kullanıcı kaydı → o kullanıcıda `role=Customer` gelmeli.
5. Uygulamayı yeniden başlat → seeder hata vermemeli (idempotent), duplicate
   kullanıcı/rol oluşmamalı.

## Yorumlar

Kod yorumları Türkçe (proje konvansiyonu).