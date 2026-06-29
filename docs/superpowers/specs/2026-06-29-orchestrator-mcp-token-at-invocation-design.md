# Orchestrator MCP Auth — Handler-Level Authorization Design

**Tarih:** 2026-06-30 (rev3)
**Durum:** Tasarım onaylandı
**Bağlam:** Chat widget doğrulamasında agent'lar boş tool ile çalışıyor. Bu spec, "request-aware agent" ve "per-tool MCP guard" yaklaşımlarının yerini alır. rev3: orchestrator'ın kendi m2m provider'ı kaldırıldı (WebApp zaten token gönderiyor); MCP auth tek noktada (handler middleware).

## 1. Amaç

Agent'lar MCP tool'larını gerçekten kullanabilsin; yetki **hem REST hem MCP'nin geçtiği tek noktada (Wolverine handler middleware)** ve **WebApp'in gönderdiği token'la** kontrol edilsin.

## 2. Temel içgörü — token'ı WebApp sağlıyor

Orchestrator MCP'leri yalnız WebApp BFF proxy'sinden gelen isteklerle kullanır. `ChatEndpoints.cs` her zaman bir token gönderir:
- **Anonim:** `tokenService.GetClientAccessTokenAsync()` → ecommerce.bff client_credentials (`catalog.read discount.read`, audience `catalog.api`/`discount.api`).
- **Login:** kullanıcının access token'ı (kullanıcı scope'ları + audience'ları).

Bu token `Authorization` header'ında orchestrator'a gelir. Dolayısıyla orchestrator **kendi m2m token'ını üretmez**; gelen token'ı MCP çağrılarına **forward eder**.

## 3. Neden handler-level (per-tool guard / request-aware agent yerine)

MCP tool'ları `bus.InvokeAsync(command)` ile **doğrudan handler'ı** çağırıp REST endpoint'inin `.RequireAuthorization(scope)`'unu **baypas ediyor** (in-process bus çağrısında AuthorizationMiddleware çalışmaz). REST ve MCP'nin tek ortak noktası **handler**'dır → yetki oraya konur: tek doğruluk kaynağı, tekrarsız, singleton agent doğru kalır.

## 4. Tasarım

### 4a. `RequiredScopeAttribute` (Common)
Komut/sorguya gereken scope'u işaretler: `[RequiredScope(AuthorizationScopes.CatalogWrite)]`.

### 4b. `ScopeAuthorizationMiddleware` (Wolverine, Common)
Her handler'dan önce çalışır; mesaj tipindeki `[RequiredScope]`'u okur, `IHttpContextAccessor.User.HasScope` ile kontrol eder; yoksa `UnauthorizedAccessException`. `opts.Policies.AddMiddleware(...)` ile catalog ve basket'e kayıtlı.

### 4c. Komut/sorgu anotasyonları (catalog, basket)
- Catalog: get/search → `catalog.read`; create/update/delete → `catalog.write`.
- Basket: get → `basket.read`; add/remove/coupon → `basket.write`.

### 4d. Orchestrator: gelen token'ı forward et (DelegatingHandler)
- `TokenInjectingHandler`: MCP'ye giden her isteğe **o anki isteğin `Authorization` header'ını** ekler. m2m üretimi YOK (WebApp sağlıyor).
- Açılışta (boot, istek yok) header yoktur → keşif **token'sız (anonim)** yapılır.
- `McpToolProvider` yalnız `ListToolsAsync`. Agent'lar Singleton; tool'lar açılışta bir kez keşfedilir.
- Orchestrator'da IdentityServer config / Duende paketi / m2m provider **YOK**.

### 4e. MCP transport açık (auth tek noktada)
- **Gateway `/mcp/*` route'ları:** `AuthorizationPolicy` YOK (yalnız yönlendirir). [Not: gateway `Audience=gateway.api` olduğu ve böyle bir ApiResource olmadığı için gateway forward edilen hiçbir token'ı doğrulayamaz; bu yüzden /mcp route'larında auth olamaz.]
- **Catalog/Basket `MapMcp("/mcp")`:** `.RequireAuthorization()` YOK → açılış keşfi (ListTools) token'sız çalışır. `UseAuthentication` global çalıştığı için, token GELDİĞİNDE `HttpContext.User` yine dolar → handler middleware scope'u kontrol eder.
- **Tek MCP auth noktası = handler middleware (`[RequiredScope]`).**
- REST endpoint `.RequireAuthorization(scope)`'ları KALIR (REST için ayrı katman; MCP'yi etkilemez).

## 5. Veri akışı

**Anonim arama (read):**
```
WebApp /chat/stream (anonim) → tokenService m2m (catalog.read) → orchestrator /public/v1/responses (Authorization: m2m)
  → search_products tool → TokenInjectingHandler: gelen m2m token'i forward
  → gateway /mcp/catalog (anonim route) → Catalog MCP (transport acik; UseAuthentication User'i doldurur)
     → bus.InvokeAsync(GetProductByNameQuery) → ScopeAuthorizationMiddleware: catalog.read ✓ → urunler
```

**Açılış keşfi:**
```
boot → singleton agent factory → CollectTools → ListTools (token YOK)
  → gateway /mcp/catalog (anonim) → Catalog MCP (transport acik, ListTools anonim) → tool listesi
```

**Login sepete ekleme:**
```
WebApp /chat/stream (login) → user token → orchestrator /assistant/v1/responses (Authorization: user)
  → add_to_cart → TokenInjectingHandler: user token forward → Basket MCP
     → ScopeAuthorizationMiddleware: basket.write (kullanicida) ✓ → sepete eklenir
```

## 6. Güvenlik

- MCP yetkisi tamamen **handler middleware**'de (scope). Gateway/transport yalnız yönlendirir.
- ListTools anonim (yalnız metadata, hassas değil).
- Anonim CallTool (token'sız) → `HttpContext.User` anonim → `HasScope` false → reddedilir. Gerçek anonim çağrılar WebApp m2m token'ıyla gelir (catalog.read var → okuma çalışır, write yok).
- Login → kullanıcı scopeّ'ları.

## 7. Komponentler (dosyalar)

| Dosya | Sorumluluk | İşlem |
|------|-----------|------|
| `src/Common/Utils/Authorization/RequiredScopeAttribute.cs` | scope işareti | (yapıldı) |
| `src/Common/Utils/Authorization/ScopeAuthorizationMiddleware.cs` | Wolverine scope kontrolü | (yapıldı) |
| `src/services/catalog/.../Features/**` + `Program.cs` | `[RequiredScope]` + middleware kaydı | (yapıldı) |
| `src/services/basket/.../Features/**` + `Program.cs` | `[RequiredScope]` + middleware kaydı | (yapıldı) |
| `src/services/catalog/Catalog.Api/Program.cs` | `MapMcp` `RequireAuthorization` KALDIR | Modify |
| `src/services/basket/Basket.Api/Program.cs` | `MapMcp` `RequireAuthorization` KALDIR | Modify |
| `src/services/gateway/Gateway/appsettings.Development.json` | `/mcp/*` AuthorizationPolicy KALDIR | (yapıldı, kullanıcı) |
| `src/AgentOrchestrator/TokenInjectingHandler.cs` | yalnız gelen token'ı forward | Modify |
| `src/AgentOrchestrator/ClientCredentialsTokenProvider.cs` | — | **Delete** |
| `src/AgentOrchestrator/appsettings.json` + `.csproj` + `Program.cs` | IdentityServer config + Duende + provider kaydı KALDIR | Modify |

## 8. Doğrulama (runtime; TDD kapsam dışı)

- **D1:** Anonim "Nike ürünleri ara" → gerçek ürün; `search_products` çağrısı; `response.failed` yok; startup catch yok.
- **D2:** Anonim `create_product` → reddedilir (WebApp m2m token'ında `catalog.write` yok → middleware).
- **D3:** Login → "sepete ekle" → kullanıcı token'ıyla çalışır.
- **D4:** Açılış keşfi token'sız geçer (dashboard'da MCP 401 warning'i yok; agent tool'lara sahip).
- **D5:** Streaming + çok turlu hafıza bozulmaz.

## 9. Kapsam dışı / açık konular

- Gateway'in `Audience=gateway.api` yanlış yapılandırması (REST proxy route'larını da etkiler) — ayrı, MCP dışı.
- Basket tool'larının açılışta keşfi: ListTools anonim çalışır (transport açık); çağrı login token'ı ister. Per-user davranış login isteğinde token forward ile sağlanır.
- `UnauthorizedAccessException`'ın 403'e map'i — MCP yolu için fırlatma yeterli (agent hata olarak aktarır).