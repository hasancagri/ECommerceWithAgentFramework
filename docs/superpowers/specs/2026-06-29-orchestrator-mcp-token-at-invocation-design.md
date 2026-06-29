# Orchestrator MCP Auth — Handler-Level Authorization Design

**Tarih:** 2026-06-29 (rev2)
**Durum:** Tasarım onaylandı
**Bağlam:** Chat widget doğrulamasında agent'lar boş tool ile çalışıyor (singleton, açılışta token'sız toplanan tool'lar 401). Bu spec, daha önce yazılan "request-aware agent" ve "per-tool MCP guard" yaklaşımlarının yerini alır.

## 1. Amaç

Agent'lar MCP tool'larını gerçekten kullanabilsin; yetki **hem REST hem MCP'nin geçtiği tek noktada (Wolverine handler'ı) ve doğru token'la** kontrol edilsin:
- **Token edinimi:** orchestrator her MCP çağrısına token iliştirir — isteğin kullanıcı token'ı varsa o, yoksa **m2m** (client_credentials, read scope'ları).
- **Yetki:** komut/sorgu üzerindeki `[RequiredScope]` attribute'una göre, bir **Wolverine middleware** `HttpContext.User`'daki scope claim'ini kontrol eder. Bu middleware her handler invocation'ında çalışır → REST ve MCP yolları **tek noktada** korunur.
- **Anonim (public):** m2m token → yalnız read scope'ları → okuma çalışır, yazma reddedilir.
- **Login (assistant):** kullanıcı token'ı → kullanıcının scope'ları → read + write + basket.

## 2. Neden handler-level (per-tool guard / request-aware agent yerine)

**Kritik bulgu:** MCP tool'ları `bus.InvokeAsync(command)` ile **doğrudan handler'ı** çağırıyor; bu, REST endpoint'ini ve oradaki `.RequireAuthorization(scope)`'u **baypas ediyor** (RequireAuthorization bir ASP.NET endpoint metadata'sı; in-process bus çağrısında AuthorizationMiddleware çalışmaz). Yani REST ve MCP'nin **tek ortak noktası handler'dır.** Yetkiyi oraya koymak:
- **Tek doğruluk kaynağı:** her giriş noktası (REST, MCP, ileride başka) aynı kontrolden geçer.
- **Tekrarsız:** her tool'a/endpoint'e elle guard yazmak yok; attribute + tek middleware.
- **Singleton agent doğru kalır:** tool'lar açılışta bir kez keşfedilir; token HTTP katmanında akar.

## 3. Mevcut durum / bulgular

- Orchestrator agent'ları `Singleton` (kütüphane açılışta yakalıyor, [[orchestrator-agent-singleton]]); tool'lar açılışta token'sız toplandığı için boş.
- MCP server (`Catalog.Api/Program.cs`): `app.MapMcp("/mcp").RequireAuthorization()` = transport kapısı (herhangi geçerli token). Per-tool scope kontrolü yorumda var ama **kodda yok** (`ProductMcpTools`/`BasketMcpTools` hiç scope kontrol etmiyor) → MCP yolu scope'u baypas ediyor.
- REST endpoint'leri scope'u `.RequireAuthorization(AuthorizationScopes.X)` ile kontrol ediyor (örn. `DeleteProduct.cs:47`).

## 4. Tasarım

### 4a. `RequiredScopeAttribute` (Common)
Komut/sorgu sınıfına konan, gereken scope'u taşıyan basit attribute:
```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiredScopeAttribute(string scope) : Attribute { public string Scope { get; } = scope; }
```
Örnek: `[RequiredScope(AuthorizationScopes.CatalogWrite)] public record DeleteProductCommand(Guid Id);`

### 4b. `ScopeAuthorizationMiddleware` (Wolverine, Common)
Her handler'dan ÖNCE çalışır; mesajın `[RequiredScope]`'unu okur, `IHttpContextAccessor.User.HasScope` ile kontrol eder; yoksa `UnauthorizedAccessException` fırlatır (handler çalışmaz).
- Mesaj tipine `[RequiredScope]` yoksa → geç (scope istemeyen handler'lar etkilenmez).
- `HasScope` = Task'taki helper (token'daki `"scope"` claim'i; `MapInboundClaims=false` ile aynı semantik).
- Wolverine'e `opts.Policies.AddMiddleware(typeof(ScopeAuthorizationMiddleware))` ile kaydedilir — yalnız **catalog ve basket** servislerinde (MCP'ye açık olanlar).

### 4c. Komut/sorgu anotasyonları (catalog, basket)
MCP'den erişilebilen komut/sorgulara `[RequiredScope]`:
- Catalog: `get_product`/`search_products` → `catalog.read`; `create/update/delete_product` → `catalog.write`.
- Basket: `get_basket` → `basket.read`; `add_to_cart`/`remove_basket_item`/`apply/remove_discount_coupon` → `basket.write`.

### 4d. Orchestrator: per-invocation token (DelegatingHandler)
- MCP `HttpClient`'ına **`TokenInjectingHandler`**: her giden MCP isteğine token ekler — `IHttpContextAccessor`'da kullanıcı token'ı varsa o, yoksa `IClientCredentialsTokenProvider`'dan **m2m** token (cache'li). Açılış keşfi de m2m token'la geçer.
- `McpToolProvider` sadeleşir: token okuma kalkar (handler'a taşınır); sadece `ListToolsAsync`.
- Agent'lar **Singleton kalır**; tool'lar açılışta bir kez keşfedilir.
- `ClientCredentialsTokenProvider` + orchestrator IdentityServer config (`m2m.client`/`dev-secret`, `catalog.read`).

### 4e. Defense-in-depth — değişMEYENler
- **MCP transport kapısı KALIR:** `MapMcp("/mcp").RequireAuthorization()` (katman 1: authenticated). Orchestrator her çağrıya token iliştirdiği için keşif/çağrı token'lı geçer; transport açmaya gerek YOK.
- **Gateway `/mcp/*` policy'leri KALIR** (authenticated token şart). Orchestrator token gönderdiği için geçer.
- **REST endpoint `.RequireAuthorization(scope)`'ları KALIR** (defense-in-depth; handler middleware ile çift katman).

## 5. Veri akışı

**Anonim arama (read):**
```
POST /public/v1/responses (token yok)
  → singleton public agent search_products tool → MCP HttpClient
     → TokenInjectingHandler: HttpContext yok → m2m token (catalog.read)
     → gateway /mcp/catalog (authenticated ✓) → Catalog MCP (transport authenticated ✓)
        → bus.InvokeAsync(GetProductByNameQuery) → ScopeAuthorizationMiddleware:
           [RequiredScope(catalog.read)] var, m2m'de catalog.read var ✓ → handler → ürünler
```

**Anonim yazma (deny):**
```
... create_product → m2m token (catalog.read, write YOK)
  → ScopeAuthorizationMiddleware: [RequiredScope(catalog.write)] ✗ → UnauthorizedAccessException → reddedilir
```

**Login sepete ekleme:**
```
POST /assistant/v1/responses (Authorization: user token)
  → add_to_cart → TokenInjectingHandler: user token forward
  → ScopeAuthorizationMiddleware: [RequiredScope(basket.write)] kullanicida var ✓ → sepete eklenir
```

## 6. Güvenlik

- **Katman 1 (transport + gateway):** authenticated token şart → anonim MCP erişimi yok.
- **Katman 2 (handler middleware):** `[RequiredScope]` → ince scope kontrolü, REST + MCP ortak.
- m2m yalnız read scope'ları → anonim **read-only**; login **per-user**.
- Hata: `UnauthorizedAccessException` fırlatılır → MCP'de hata sonucu, REST'te exception handler ile uygun yanıt (servislerde mevcut handler kontrol edilir; yoksa not düşülür).

## 7. Komponentler (dosyalar)

| Dosya | Sorumluluk | İşlem |
|------|-----------|------|
| `src/Common/Utils/Authorization/ScopeAuthorizationExtensions.cs` | `ClaimsPrincipal.HasScope` (mevcut) | (var) |
| `src/Common/Utils/Authorization/RequiredScopeAttribute.cs` | Komuta gereken scope'u işaretler | Create |
| `src/Common/Utils/Authorization/ScopeAuthorizationMiddleware.cs` | Wolverine `Before`: attribute → HasScope kontrolü | Create |
| `src/services/catalog/Catalog.Api/Domains/Products/Features/**` | Komut/sorgulara `[RequiredScope]` | Modify |
| `src/services/catalog/Catalog.Api/Program.cs` | Middleware'i Policies'e kaydet | Modify |
| `src/services/basket/Basket.Api/Domains/Baskets/Features/**` | Komut/sorgulara `[RequiredScope]` | Modify |
| `src/services/basket/Basket.Api/Program.cs` | Middleware'i Policies'e kaydet | Modify |
| `src/AgentOrchestrator/ClientCredentialsTokenProvider.cs` | m2m token + cache | Create |
| `src/AgentOrchestrator/TokenInjectingHandler.cs` | Per-invocation token enjeksiyonu | Create |
| `src/AgentOrchestrator/McpToolProvider.cs` | Token okuma çıkar | Modify |
| `src/AgentOrchestrator/AgentOrchestrator.csproj` + `appsettings.json` + `Program.cs` | Duende paketi + identity config + DI kaydı | Modify |

**Geri alınacak:** `ProductMcpTools.cs`'teki per-tool guard'lar (yanlış yaklaşım commit'i) eski haline döner; `MapMcp` `RequireAuthorization` korunur.

## 8. Doğrulama (runtime; TDD kapsam dışı)

- **D1:** Anonim "Nike ürünleri ara" → gerçek ürün verisi (m2m + search_products); SSE'de tool çağrısı; "lütfen bekleyin"de takılmaz.
- **D2:** Anonim `create_product` → reddedilir (m2m'de catalog.write yok; middleware `UnauthorizedAccessException`).
- **D3:** Login → "sepetimi göster"/"sepete ekle" → kullanıcı token'ıyla çalışır.
- **D4:** Streaming + çok turlu hafıza bozulmaz.
- **D5:** Keşif token'la geçer (orchestrator m2m fallback); açılışta MCP tool kesfi başarılı.
- Her görevin kapısı `dotnet build` (0 error).

## 9. Kapsam dışı / açık konular

- Wolverine `Before` middleware imzası / `Envelope` erişimi — planın ilk task'ında kodla kesinleştirilir.
- `UnauthorizedAccessException`'ın catalog/basket'te 403'e map'lenmesi — mevcut exception handler kontrol edilir; yoksa minimal ekleme not düşülür (ama MCP yolu için fırlatma yeterli).
- Diğer servisler (order/payment/...) MCP'ye açık değil → kapsam dışı.