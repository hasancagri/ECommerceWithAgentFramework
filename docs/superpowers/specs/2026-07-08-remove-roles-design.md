# Rol Geliştirmelerinin Kaldırılması — Tasarım

**Tarih:** 2026-07-08
**Durum:** Onaylandı

## Amaç

Projeye eklenen Admin/Customer **rol** kavramını (faz 1: kimlik/rol tohumlama, faz 2:
admin-only yazma zorlaması) tümüyle kaldırmak. İş bittiğinde "rol" kavramı kod tabanında
kalmayacak.

Yazma uçları **rol öncesi davranışa** döner: yalnızca `[RequiredScope]` (ör.
`AuthorizationScopes.CatalogWrite`) ile korunur — geçerli token + doğru scope yeterlidir,
ayrıca Admin rolü aranmaz.

## Kapsam Kararları (kullanıcı onaylı)

- **Her iki faz da** kaldırılır (kimlik tohumlama + servis yazma zorlaması).
- **Seed admin kullanıcısı da** kaldırılır — rolsüz bir admin kullanıcının anlamı kalmıyor.
- **DB tabloları bırakılır.** `AspNetRoles` / `AspNetUserRoles` standart ASP.NET Identity
  şemasının parçası ve `InitialIdentity` migration'ında zaten var. Boş kalırlar, zararsız.
  Yeni migration üretilmez.
- Working tree'deki **ilgisiz refactor** değişikliklerine (namespace hizalama, async migrate
  vb.) dokunulmaz.

## Değişiklikler

### Silinecek dosyalar (6)

- `src/Identity.Server/Roles.cs`
- `src/Identity.Server/Data/IdentitySeed.cs` — tek amacı rol + admin kullanıcı tohumlamak;
  komple silinir.
- `src/Common/Utils/Constants/Roles.cs`
- `src/Common/Utils/Authorization/RequiredRoleAttribute.cs`
- `src/Common/Utils/Authorization/RoleAuthorizationMiddleware.cs`

### Düzenlenecek dosyalar

**Identity.Server**

- `Program.cs`:
  - `using Identity.Server.Data;` kaldırılır (yalnızca `SeedAsync` için vardı; başka kullanım
    yoksa).
  - `await app.SeedAsync();` satırı ve üstündeki yorumu kaldırılır.
  - `AddIdentity<ApplicationUser, IdentityRole>()` **KALIR** — standart Identity iskeleti;
    tablolar bırakıldığı için değiştirilmez.
- `Pages/Account/Create/Index.cshtml.cs`:
  - `await _userManager.AddToRoleAsync(user, Roles.Customer);` satırı ve üstündeki Türkçe
    yorumu kaldırılır.

**Servisler (catalog, discount, stock)**

- Her `Program.cs`'ten `RoleAuthorizationMiddleware` kaydı kaldırılır — yani şu blok + yorumu:
  ```csharp
  // Rol yetkisi: middleware SADECE [RequiredRole] tasiyan komut/sorgulara weave edilir.
  opts.Policies.AddMiddleware(
      typeof(RoleAuthorizationMiddleware),
      chain => chain.MessageType.GetCustomAttribute<RequiredRoleAttribute>() is not null);
  ```
  (`ScopeAuthorizationMiddleware` kaydı **KALIR**.)
- Aşağıdaki 6 komut dosyasından `[RequiredRole(Roles.Admin)]` attribute satırı kaldırılır;
  bu kaldırma sonrası kullanılmayan hale gelen `using Common.Utils.Authorization;` /
  `using Common.Utils.Constants;` importları da (dosyada başka kullanımları yoksa) temizlenir:
  - catalog: `Domains/Products/Features/Commands/CreateProduct.cs`
  - catalog: `Domains/Products/Features/Commands/UpdateProduct.cs`
  - catalog: `Domains/Products/Features/Commands/DeleteProduct.cs`
  - discount: `Domains/Discounts/Features/Commands/CreateDiscount.cs`
  - stock: `Domains/Stocks/Features/Commands/IncreaseStock.cs`
  - stock: `Domains/Stocks/Features/Commands/DecreaseStock.cs`
- **`[RequiredScope(...)]` attribute'ları KALIR** — scope tabanlı yetki rol işinden bağımsızdır.

### İsteğe bağlı temizlik

- Identity.Server `appsettings*.json` içinde `SeedAdmin:Email` / `SeedAdmin:Password`
  anahtarları varsa kaldırılır (kod artık okumaz; kalması zararsız ama gereksiz).

## Dokunulmayacaklar

- DB / migration'lar (kullanıcı kararı: tabloları bırak).
- `AddIdentity<ApplicationUser, IdentityRole>()` (standart iskelet).
- Working tree'deki ilgisiz refactor değişiklikleri.

## Doğrulama

- `dotnet build ECommerceWithAgentFramework.slnx` temiz derlenmeli. Derleme, kullanılmayan
  `using`/kalan referansları da yakalar (özellikle silinen `Roles`, `RequiredRoleAttribute`,
  `RoleAuthorizationMiddleware` tiplerine dangling referans kalmadığını).
- Test projesi yok; manuel derleme birincil kontroldür.

## Sonraki Adım (bellek)

`roles-status.md` memory'si "phase 1+2 merged" diyor. İş tamamlanınca bu geliştirmelerin
2026-07-08'de geri alındığını yansıtacak şekilde güncellenir.