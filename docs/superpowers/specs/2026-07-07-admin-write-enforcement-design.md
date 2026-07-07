# Faz 2 — Admin-only Write Enforcement — Tasarım

**Tarih:** 2026-07-07
**Durum:** Onaylandı

## Amaç

Yönetimsel (back-office) write işlemlerini yalnızca `Admin` rolüne sahip
kullanıcılara açmak. Kapsam: **catalog**, **discount**, **stock** servislerinin
write işlemleri. Rol kontrolü, mevcut scope kontrolüyle aynı noktada — Wolverine
handler middleware'inde — yapılır; böylece REST ve MCP tek noktadan korunur.

Bu, rol yol haritasının 2. fazıdır. Faz 1 (rollerin + admin kullanıcının seed'i)
tamamlandı; `role` claim'i token'a akıyor.

## Kapsam

**Dahil (admin-only olacak write'lar):**
- catalog: `CreateProductCommand`, `UpdateProductCommand`, `DeleteProductCommand`
- discount: `CreateDiscountCommand`
- stock: `IncreaseStockCommand`, `DecreaseStockCommand`

**Hariç (bu fazda dokunulmaz):**
- basket / order / payment write'ları → müşteri self-servis; admin-only YAPILMAZ
  (yoksa checkout kırılır).
- Read (query) işlemleri.
- Rol→scope eşlemesi (scope mekanizmasına dokunulmaz; bu faz sadece rol katmanı
  ekler).
- ChatAgent MCP tool ayrımı (Faz 3).

## Neden handler-level (endpoint-level değil)

catalog'un `delete_product` bir MCP tool'u; MCP çağrısı REST endpoint
authorization'ını atlar. Güvenli tek nokta, hem REST hem MCP'nin uğradığı
Wolverine handler middleware'idir (`bus.InvokeAsync` ortak yol). Aynı deseni
discount/stock'a da uygulamak rol-yetkisini **tek mekanizmaya** indirir ve
ileride MCP eklerlerse otomatik korur. Bu, projenin belgelenmiş deseniyle
("authorization is handler-level: [RequiredScope] on the message record")
tutarlıdır.

## Bileşenler

### 1. `Roles` sabiti (Common)

- Yeni dosya: `src/Common/Utils/Constants/Roles.cs`, namespace
  `Common.Utils.Constants`.
- `public static class Roles { public const string Admin = "Admin"; public const string Customer = "Customer"; }`
- `AuthorizationScopes`'un yanında (aynı auth-sabit ailesi).
- Identity.Server kendi `src/Identity.Server/Roles.cs`'ini korur (Common ağır
  bağımlılıklar çektiği için IdP'yi Common'a bağlamayız). İki dosyaya da karşılıklı
  referans yorumu eklenir ki değerler senkron kalsın.

### 2. `RequiredRoleAttribute` (Common)

- Yeni dosya: `src/Common/Utils/Authorization/RequiredRoleAttribute.cs`.
- `RequiredScopeAttribute` ikizi:
  ```csharp
  [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
  public sealed class RequiredRoleAttribute(string role) : Attribute
  {
      public string Role { get; } = role;
  }
  ```

### 3. `RoleAuthorizationMiddleware` (Common)

- Yeni dosya: `src/Common/Utils/Authorization/RoleAuthorizationMiddleware.cs`.
- `ScopeAuthorizationMiddleware` ikizi:
  ```csharp
  public static class RoleAuthorizationMiddleware
  {
      public static void Before(Envelope envelope, IHttpContextAccessor http)
      {
          var role = envelope.Message?.GetType()
              .GetCustomAttribute<RequiredRoleAttribute>()?.Role;
          if (role is null)
              return;

          if (http.HttpContext?.User.HasClaim("role", role) != true)
              throw new UnauthorizedAccessException($"Required role missing: {role}");
      }
  }
  ```
- `MapInboundClaims = false` olduğu için token'daki `role` claim'i ham; `HasClaim`
  doğrudan çalışır. `UnauthorizedAccessException` mevcut GlobalExceptionHandler
  yoluyla 403'e maplenir (scope middleware ile aynı davranış).

### 4. Mesaj record'larına `[RequiredRole(Roles.Admin)]`

Yukarıdaki 6 write komutuna eklenir. Mevcut `[RequiredScope(...Write)]`
attribute'ları **korunur** (scope + rol birlikte gerekir).

### 5. Servis wiring'i (Wolverine policy)

Her serviste, `[RequiredRole]` taşıyan mesajlara `RoleAuthorizationMiddleware`
weave edilir — catalog'daki scope weave deseninin aynısı:

```csharp
opts.Policies.AddMiddleware(
    typeof(RoleAuthorizationMiddleware),
    chain => chain.MessageType.GetCustomAttribute<RequiredRoleAttribute>() is not null);
```

- **catalog:** Program.cs zaten scope middleware'i weave ediyor + HttpContextAccessor
  var → sadece yukarıdaki role weave satırı eklenir.
- **discount & stock:** Program.cs'e (a) `builder.Services.AddHttpContextAccessor();`
  (b) `builder.Host.UseWolverine(...)` içine yukarıdaki role weave. Bu servislerde
  şu an hiç Wolverine policy/HttpContextAccessor yok; eklenir.

## Hata davranışı

- Yetkisiz rol → `UnauthorizedAccessException` → GlobalExceptionHandler → 403.
- Scope eksikse zaten mevcut scope katmanı 403 döner. İki katman bağımsız; ikisi de
  geçmeli.

## Test / Doğrulama

Test altyapısı yok. Manuel:

1. `dotnet build ECommerceWithAgentFramework.slnx` — 0 hata.
2. Aspire ile ayağa kaldır.
3. **Admin** (seed) ile catalog/discount/stock write (REST) → başarılı.
4. **Customer** (self-register) ile aynı write'lar → 403.
5. **Customer** ile basket/order/payment write → hâlâ başarılı (etkilenmedi).
6. catalog `delete_product` MCP tool'u Customer token'ıyla → 403 (handler-level
   koruma MCP'de de geçerli).

## Yorumlar

Kod yorumları Türkçe (proje konvansiyonu).