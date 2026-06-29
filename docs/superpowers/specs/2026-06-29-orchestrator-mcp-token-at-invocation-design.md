# Orchestrator MCP Auth — Token-at-Invocation Design

**Tarih:** 2026-06-29
**Durum:** Tasarım onayı bekliyor
**Bağlam:** Chat widget doğrulamasında agent'lar boş tool ile çalışıyor (singleton, açılışta token'sız toplanan tool'lar 401). Bu spec, daha önce yazılan **"request-aware agent" (Option C)** tasarımının yerini alır — kullanıcının önerdiği, sistemin zaten amaçladığı (ama kurulmamış) modeldir.

## 1. Amaç

Agent'lar MCP tool'larını gerçekten kullanabilsin; yetki **çağrı anında, doğru token'la, downstream'de** kontrol edilsin:
- **Tool keşfi (ListTools):** auth'suz, açılışta bir kez (yalnız metadata).
- **Tool çağrısı (CallTool):** o an token iliştirilir; yetki MCP server'da **per-tool scope** ile kontrol edilir.
- **Anonim (public):** orchestrator'ın **m2m** (client_credentials) token'ı → yalnız read scope'ları → sadece okuma.
- **Login (assistant):** isteğin **kullanıcı token'ı** → kullanıcının scope'ları → read + write + basket.

## 2. Neden bu (Option C yerine)

| | Option C (request-aware agent) | Bu (token-at-invocation) |
|---|---|---|
| Tool toplama | Her istekte yeniden (MCP handshake/mesaj) | Açılışta bir kez (statik) |
| Token | Wrapper agent, RunCore* override | HTTP katmanı (DelegatingHandler) |
| Hız | Her mesajda keşif gecikmesi | Keşif gecikmesi yok |
| Singleton uyumu | Wrapper ile dolaylı | Doğal — singleton doğru kalır |
| Tasarım niyeti | — | MCP server'daki "per-tool scope" yorumunu birebir gerçekler |

## 3. Mevcut durum / bulgular

- Orchestrator agent'ları `Singleton` (kütüphane açılışta yakalıyor, [[orchestrator-agent-singleton]]); tool'lar açılışta token'sız toplandığı için boş.
- MCP server (`Catalog.Api/Program.cs:101`): `app.MapMcp("/mcp").RequireAuthorization()` = transport kapısı (herhangi geçerli token). Yorum "read/write ayrımı tool'ların içinde (RequireScope)" diyor ama **`ProductMcpTools.cs`'te hiç scope kontrolü yok** → MCP yolu scope'u baypas ediyor (REST yolu etmiyor). Yazılmamış niyet.

## 4. Tasarım — üç parça

### 4a. MCP server'lar (catalog, basket): per-tool scope enforcement
- Her tool gerekli scope'u kontrol eder: `IHttpContextAccessor` → `User`'ın `scope` claim'i.
- Ortak yardımcı: `EnsureScope(accessor, AuthorizationScopes.CatalogRead)` — yoksa LLM'in aktarabileceği **yapısal yetkisiz sonuç** döner (exception fırlatmak yerine `FeatureObjectResultModel` hata sonucu tercih; UX: "bu işlem için giriş/yetki gerekli").
- Tercih: MCP SDK tool-invocation **filter**'ı destekliyorsa tek yerde `tool adı → scope` eşlemesi; desteklemiyorsa her tool içinde inline çağrı. (Planlamada SDK yeteneği sabitlenecek.)
- Scope eşlemesi: `get_product`/`search_products` → `catalog.read`; `create/update/delete_product` → `catalog.write`. Basket tool'ları → `basket.read`/`basket.write`.
- `MapMcp("/mcp")`'den `.RequireAuthorization()` **kaldırılır**; `UseAuthentication` kalır (token varsa `User` dolar). Keşif anonim; çağrı scope ister.

### 4b. Gateway: `/mcp/*` route'ları anonim
- `catalog-mcp-route`, `basket-mcp-route`'tan `AuthorizationPolicy` kaldırılır. Gateway token'ı (varsa) downstream'e geçirir, yoksa anonim geçirir; gerçek yetki MCP server'da.

### 4c. Orchestrator: per-invocation token (DelegatingHandler)
- MCP `HttpClient`'ına **`TokenInjectingHandler`** eklenir. Her giden MCP isteğinde:
  - `IHttpContextAccessor.HttpContext`'te kullanıcı `Authorization`'ı varsa → **forward**.
  - Yoksa → `IClientCredentialsTokenProvider`'dan **m2m** token (cache'li).
- `McpToolProvider` **sadeleşir**: token okuma kalkar (handler'a taşınır); sadece MCP client kurar + `ListToolsAsync`.
- Agent'lar **Singleton kalır** (doğru ve kütüphane uyumlu). Tool'lar açılışta bir kez keşfedilir; token her çağrıda handler ile akar. Singleton ↔ per-request gerilimi çözülür.
- `ClientCredentialsTokenProvider` + orchestrator IdentityServer config (authority + `m2m.client`/`dev-secret`, scope `catalog.read` vb.) eklenir.

## 5. Veri akışı

**Anonim arama:**
```
POST /public/v1/responses (token yok)
  → singleton public agent (statik tool'lar) search_products tool çağırır
     → MCP HttpClient → TokenInjectingHandler: HttpContext token yok → m2m token (catalog.read)
     → gateway /mcp/catalog (anonim route) → Catalog MCP
        → EnsureScope(catalog.read) ✓ (m2m'de var) → ürünler döner
```

**Login sepete ekleme:**
```
POST /assistant/v1/responses (Authorization: user token)
  → assistant agent add_to_basket tool çağırır
     → TokenInjectingHandler: HttpContext'te user token → forward
     → gateway /mcp/basket → Basket MCP
        → EnsureScope(basket.write) ✓ (kullanıcıda var) → sepete eklenir
```

## 6. Güvenlik

- **Keşif anonim** = yalnız metadata (tool şeması), hassas değil.
- **m2m scope'ları yalnız read** (`catalog.read`/`discount.read`/`stock.read`) → anonim kullanıcı **yazamaz** (`create/update/delete` → `catalog.write` ister, m2m'de yok → EnsureScope reddeder).
- **Login** = kullanıcının scope'ları kadar.
- Doğal sonuç: anonim **read-only**, login **per-user**. Tasarımın hedeflediği davranış.

## 7. Komponentler (dosyalar)

| Dosya | Sorumluluk | İşlem |
|------|-----------|------|
| `src/services/catalog/Catalog.Api/Domains/Products/ProductMcpTools.cs` | Her tool'a scope kontrolü | Modify |
| `src/services/catalog/Catalog.Api/Program.cs` | `MapMcp` transport kapısını kaldır | Modify |
| `src/services/basket/Basket.Api/Domains/Baskets/BasketMcpTools.cs` + `Program.cs` | Aynısı (basket scope'ları) | Modify |
| `src/Common/...` | `EnsureScope` yardımcı (ortak) | Create |
| `src/services/gateway/Gateway/appsettings.Development.json` | `*-mcp-route` AuthorizationPolicy kaldır | Modify |
| `src/AgentOrchestrator/TokenInjectingHandler.cs` | Per-invocation token enjeksiyonu | Create |
| `src/AgentOrchestrator/ClientCredentialsTokenProvider.cs` | m2m token edin + cache | Create |
| `src/AgentOrchestrator/McpToolProvider.cs` | Token okuma çıkar; sadece ListTools | Modify |
| `src/AgentOrchestrator/Program.cs` | Handler + token provider + identity config kaydı | Modify |
| `src/AgentOrchestrator/appsettings*.json` / user-secrets | IdentityServer authority + m2m | Modify |

## 8. Doğrulama (runtime; TDD kapsam dışı)

- **D1:** Anonim "Nike Air Max 1 var mı?" → gerçek ürün verisi döner (m2m + search_products); SSE'de tool çağrısı görülür, "lütfen bekleyin"de takılmaz.
- **D2:** Anonim `create_product` denemesi → yetkisiz (m2m'de catalog.write yok).
- **D3:** Login → "şunu sepete ekle" → basket tool kullanıcı token'ıyla çalışır.
- **D4:** Streaming + çok turlu hafıza bozulmaz.
- **D5:** Keşif anonim çalışır (orchestrator açılışta tool'ları token'sız toplar).
- Her görevin kapısı `dotnet build` (0 error).

## 9. Kapsam dışı / açık konular

- MCP tool-invocation **filter** mı yoksa per-tool inline `EnsureScope` mi (SDK yeteneğine göre planlamada sabitlenir).
- `EnsureScope`'un ortak yeri (`Common`) ve yetkisiz sonuç tipi.
- Token cache detayları (expiry payı, eşzamanlı yenileme).
- Gateway `Password`/`ClientCredential` policy'lerinin diğer (REST) route'lardaki geleceği — bu spec yalnız `/mcp/*`'i anonimleştirir.