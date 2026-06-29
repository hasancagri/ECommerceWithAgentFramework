# Orchestrator MCP Handler-Level Authorization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agent'lar MCP tool'larını gerçekten kullanabilsin; yetki hem REST hem MCP'nin geçtiği tek noktada (Wolverine handler middleware) ve doğru token'la kontrol edilsin.

**Architecture:** Komut/sorgulara `[RequiredScope]` attribute; bir Wolverine `Before` middleware her handler'dan önce token'daki scope claim'ini kontrol eder (REST + MCP ortak nokta). Orchestrator, MCP `HttpClient`'ına bir `DelegatingHandler` takıp her çağrıya token iliştirir (isteğin kullanıcı token'ı, yoksa m2m). MCP transport ve gateway auth'u defense-in-depth olarak KALIR.

**Tech Stack:** ASP.NET Core (net10.0/net9.0), WolverineFx (handler middleware), ModelContextProtocol, Duende.IdentityModel.Client, .NET Aspire.

**Spec:** `docs/superpowers/specs/2026-06-29-orchestrator-mcp-token-at-invocation-design.md`

## Global Constraints

- Hedef framework: net10.0 (servisler/orchestrator), Common net9.0.
- **Test/TDD bu planın dışında** (kullanıcı kararı). Her görevin kapısı `dotnet build` (0 error). Uçtan uca doğrulama Task 7'de (çalışan Aspire stack; identity `http://localhost:5001`).
- **Defense-in-depth korunur:** MCP `MapMcp().RequireAuthorization()`, gateway `/mcp/*` policy'leri, REST `.RequireAuthorization(scope)` — hiçbiri kaldırılmaz.
- `scope` claim semantiği mevcut JWT auth ile aynı (`MapInboundClaims=false`; `HasScope` helper Task 0'da mevcut).
- Hardcode port yok; identity `http://localhost:5001` (dev).

**Build komutları (mutlak yol):**
- Common: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/Common/Common.csproj`
- Catalog: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/services/catalog/Catalog.Api/Catalog.Api.csproj`
- Basket: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/services/basket/Basket.Api/Basket.Api.csproj`
- Orchestrator: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/AgentOrchestrator/AgentOrchestrator.csproj`

**Önceki durum:** `Common.ScopeAuthorizationExtensions.HasScope(this ClaimsPrincipal?, string)` zaten mevcut (commit'li). Catalog per-tool guard commit'i Task 1'de geri alınır.

---

## File Structure

| Dosya | Sorumluluk | İşlem |
|------|-----------|------|
| `src/Common/Utils/Authorization/RequiredScopeAttribute.cs` | Komuta gereken scope'u işaretler | Create |
| `src/Common/Utils/Authorization/ScopeAuthorizationMiddleware.cs` | Wolverine `Before`: attribute → HasScope | Create |
| `src/services/catalog/.../Features/{Queries,Commands}/*.cs` | Record'lara `[RequiredScope]` | Modify |
| `src/services/catalog/Catalog.Api/Program.cs` | Middleware'i Policies'e kaydet | Modify |
| `src/services/basket/.../Features/{Queries,Commands}/*.cs` | Record'lara `[RequiredScope]` | Modify |
| `src/services/basket/Basket.Api/Program.cs` | Middleware'i Policies'e kaydet | Modify |
| `src/AgentOrchestrator/ClientCredentialsTokenProvider.cs` | m2m token + cache | Create |
| `src/AgentOrchestrator/TokenInjectingHandler.cs` | Per-invocation token | Create |
| `src/AgentOrchestrator/McpToolProvider.cs` | Token okuma çıkar | Modify |
| `src/AgentOrchestrator/AgentOrchestrator.csproj` + `appsettings.json` + `Program.cs` | Duende + identity config + DI | Modify |

---

## Task 1: Per-tool guard commit'ini geri al

**Files:** (git revert — `ProductMcpTools.cs` ve `Catalog.Api/Program.cs` eski haline döner)

- [ ] **Step 1: Yanlış yaklaşım commit'ini geri al**

```bash
cd /Users/macbook/Desktop/ECommerceWithAgentFramework
git revert --no-edit "$(git log --grep='per-tool scope authz on MCP tools' --format=%H -n 1)"
```

- [ ] **Step 2: Doğrulama**

`ProductMcpTools.cs` guard'sız (orijinal) ve `Catalog.Api/Program.cs:101` tekrar `app.MapMcp("/mcp").RequireAuthorization();` olmalı:
```bash
grep -n "RequireAuthorization\|HasScope" src/services/catalog/Catalog.Api/Program.cs src/services/catalog/Catalog.Api/Domains/Products/ProductMcpTools.cs
```
Expected: `Program.cs`'te `MapMcp(...).RequireAuthorization()` var; `ProductMcpTools.cs`'te `HasScope` YOK.

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/services/catalog/Catalog.Api/Catalog.Api.csproj`
Expected: Build succeeded, 0 Error.

---

## Task 2: Common — RequiredScope attribute + Wolverine middleware

**Files:**
- Create: `src/Common/Utils/Authorization/RequiredScopeAttribute.cs`
- Create: `src/Common/Utils/Authorization/ScopeAuthorizationMiddleware.cs`

**Interfaces:**
- Consumes: `Common.ScopeAuthorizationExtensions.HasScope` (mevcut), `Wolverine.Envelope`, `Microsoft.AspNetCore.Http.IHttpContextAccessor` (Common zaten referanslıyor).
- Produces: `Common.RequiredScopeAttribute(string scope)` (Class hedefli); `Common.ScopeAuthorizationMiddleware.Before(Envelope, IHttpContextAccessor)`.

- [ ] **Step 1: RequiredScopeAttribute.cs oluştur**

```csharp
namespace Common;

// Bir komut/sorgu'nun (Wolverine message) calismasi icin gereken scope'u isaretler.
// ScopeAuthorizationMiddleware bunu okuyup token'daki scope ile karsilastirir.
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequiredScopeAttribute(string scope) : Attribute
{
    public string Scope { get; } = scope;
}
```

- [ ] **Step 2: ScopeAuthorizationMiddleware.cs oluştur**

```csharp
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Wolverine;

namespace Common;

// Wolverine middleware: her handler'dan ONCE calisir. Mesaj tipinde [RequiredScope] varsa,
// forward edilen token'daki "scope" claim'ini kontrol eder; yoksa UnauthorizedAccessException
// (handler calismaz). REST ve MCP ikisi de bus.InvokeAsync ile ayni handler'a ugradigi icin
// yetki TEK NOKTADA kontrol edilir.
public static class ScopeAuthorizationMiddleware
{
    public static void Before(Envelope envelope, IHttpContextAccessor http)
    {
        var scope = envelope.Message?.GetType()
            .GetCustomAttribute<RequiredScopeAttribute>()?.Scope;
        if (scope is null)
            return;

        if (http.HttpContext?.User.HasScope(scope) != true)
            throw new UnauthorizedAccessException($"Required scope missing: {scope}");
    }
}
```

- [ ] **Step 3: Build doğrulaması (Wolverine `Before`/`Envelope` imzasını da sabitler)**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/Common/Common.csproj`
Expected: Build succeeded, 0 Error. (Hata olursa: `Envelope` namespace `Wolverine`'de; `Envelope.Message` `object?`.)

- [ ] **Step 4: Commit**

```bash
git add src/Common/Utils/Authorization/RequiredScopeAttribute.cs src/Common/Utils/Authorization/ScopeAuthorizationMiddleware.cs
git commit -m "feat(common): RequiredScope attribute + Wolverine scope authz middleware" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Catalog — komutlara `[RequiredScope]` + middleware kaydı

**Files:**
- Modify (record üstüne attribute): `GetProductById.cs:9`, `GetProductByName.cs:9`, `CreateProduct.cs:12`, `UpdateProduct.cs:11`, `DeleteProduct.cs:9` (hepsi `src/services/catalog/Catalog.Api/Domains/Products/Features/...`)
- Modify: `src/services/catalog/Catalog.Api/Program.cs` (UseWolverine bloğu ~25-53)

**Interfaces:**
- Consumes: `Common.RequiredScopeAttribute`, `Common.Utils.Constants.AuthorizationScopes`, `Common.ScopeAuthorizationMiddleware`.

- [ ] **Step 1: Read query/command'lara attribute ekle (catalog.read)**

`Features/Queries/GetProductById.cs` — `public record GetProductByIdQuery(Guid Id);` satırının ÜSTÜNE:
```csharp
    [RequiredScope(AuthorizationScopes.CatalogRead)]
```
`Features/Queries/GetProductByName.cs` — `public record GetProductByNameQuery(...)` üstüne:
```csharp
    [RequiredScope(AuthorizationScopes.CatalogRead)]
```

- [ ] **Step 2: Write command'lara attribute ekle (catalog.write)**

`Features/Commands/CreateProduct.cs` (`public record CreateProductCommand(`), `UpdateProduct.cs` (`public record UpdateProductCommand(`), `DeleteProduct.cs` (`public record DeleteProductCommand(Guid Id);`) — her birinin record satırının ÜSTÜNE:
```csharp
    [RequiredScope(AuthorizationScopes.CatalogWrite)]
```

Her dosyada `using Common;` ve `using Common.Utils.Constants;` mevcut olmalı (DeleteProduct.cs'te var); yoksa ekle.

- [ ] **Step 3: Program.cs — middleware'i kaydet**

`src/services/catalog/Catalog.Api/Program.cs` içinde `builder.Host.UseWolverine(opts => { ... })` bloğunda, `opts.Policies.UseDurableLocalQueues();` satırının yanına ekle:
```csharp
    // Handler-level yetki: [RequiredScope] tasiyan komut/sorgular icin token scope kontrolu.
    opts.Policies.AddMiddleware(typeof(Common.ScopeAuthorizationMiddleware));
```

- [ ] **Step 4: Build doğrulaması**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/services/catalog/Catalog.Api/Catalog.Api.csproj`
Expected: Build succeeded, 0 Error. (`AddMiddleware(Type)` Wolverine `PolicyExpression`'da; hata olursa `AddMiddleware<T>()` generic overload'u kullan.)

- [ ] **Step 5: Commit**

```bash
git add src/services/catalog/Catalog.Api
git commit -m "feat(catalog): handler-level scope authz via RequiredScope + Wolverine middleware" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Basket — komutlara `[RequiredScope]` + middleware kaydı

**Files:**
- Modify: `GetBasket.cs:8` (read), `AddBasketItem.cs:8`, `DeleteBasketItem.cs:8`, `ApplyDiscountCoupon.cs:8`, `RemoveDiscountCoupon.cs:8` (write) — hepsi `src/services/basket/Basket.Api/Domains/Baskets/Features/...`
- Modify: `src/services/basket/Basket.Api/Program.cs` (UseWolverine ~19-32)

- [ ] **Step 1: get_basket sorgusuna attribute (basket.read)**

`Features/Queries/GetBasket.cs` — `public record GetBasketQuery(Guid UserId);` üstüne:
```csharp
    [RequiredScope(AuthorizationScopes.BasketRead)]
```

- [ ] **Step 2: Write command'lara attribute (basket.write)**

`Features/Commands/AddBasketItem.cs` (`public record AddBasketItemCommand(`), `DeleteBasketItem.cs` (`public record DeleteBasketItemCommand(Guid UserId, Guid Id);`), `ApplyDiscountCoupon.cs` (`public record ApplyDiscountCouponCommand(...)`), `RemoveDiscountCoupon.cs` (`public record RemoveDiscountCouponCommand(Guid UserId);`) — her record satırının üstüne:
```csharp
    [RequiredScope(AuthorizationScopes.BasketWrite)]
```

Her dosyada `using Common;` ve `using Common.Utils.Constants;` olduğundan emin ol; yoksa ekle.

- [ ] **Step 3: Program.cs — middleware'i kaydet**

`src/services/basket/Basket.Api/Program.cs` içinde `UseWolverine(opts => {...})` bloğunda `opts.Policies.UseDurableLocalQueues();` yanına:
```csharp
    opts.Policies.AddMiddleware(typeof(Common.ScopeAuthorizationMiddleware));
```

- [ ] **Step 4: Build doğrulaması**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/services/basket/Basket.Api/Basket.Api.csproj`
Expected: Build succeeded, 0 Error.

- [ ] **Step 5: Commit**

```bash
git add src/services/basket/Basket.Api
git commit -m "feat(basket): handler-level scope authz via RequiredScope + Wolverine middleware" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Orchestrator — m2m client_credentials token provider

**Files:**
- Modify: `src/AgentOrchestrator/AgentOrchestrator.csproj` (Duende.IdentityModel)
- Create: `src/AgentOrchestrator/ClientCredentialsTokenProvider.cs`
- Modify: `src/AgentOrchestrator/appsettings.json` (IdentityServer)
- Modify: `src/AgentOrchestrator/Program.cs` (identity HttpClient + provider kaydı)

**Interfaces:**
- Produces: `IClientCredentialsTokenProvider.GetTokenAsync(CancellationToken) -> Task<string?>` (Task 6 kullanır). DI: Singleton.

- [ ] **Step 1: Duende.IdentityModel paketini ekle**

`src/AgentOrchestrator/AgentOrchestrator.csproj` paket `<ItemGroup>`'una:
```xml
        <PackageReference Include="Duende.IdentityModel" />
```

- [ ] **Step 2: ClientCredentialsTokenProvider.cs oluştur**

```csharp
using Duende.IdentityModel.Client;

namespace AgentOrchestrator;

public interface IClientCredentialsTokenProvider
{
    Task<string?> GetTokenAsync(CancellationToken ct = default);
}

// m2m (client_credentials) token'i edinir + suresine gore RAM'de cache'ler.
// Anonim MCP cagrilari icin uygulamanin kendi kimligi (m2m.client, read scope'lari).
public sealed class ClientCredentialsTokenProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<ClientCredentialsTokenProvider> logger) : IClientCredentialsTokenProvider
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _token;

        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _token;

            var authority = configuration["IdentityServer:Authority"]
                ?? throw new InvalidOperationException("IdentityServer:Authority is not set");

            var client = httpClientFactory.CreateClient("identity");
            var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = $"{authority.TrimEnd('/')}/connect/token",
                ClientId = configuration["IdentityServer:ClientId"] ?? "m2m.client",
                ClientSecret = configuration["IdentityServer:ClientSecret"] ?? "dev-secret",
                Scope = configuration["IdentityServer:Scope"] ?? "catalog.read",
            }, ct);

            if (response.IsError)
            {
                logger.LogWarning("m2m token alinamadi: {Error}", response.Error);
                return null;
            }

            _token = response.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, response.ExpiresIn - 60));
            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }
}
```

- [ ] **Step 3: appsettings.json'a IdentityServer config ekle**

`src/AgentOrchestrator/appsettings.json` kök objesine (mevcut JSON'a virgülle):
```json
  "IdentityServer": {
    "Authority": "http://localhost:5001",
    "ClientId": "m2m.client",
    "ClientSecret": "dev-secret",
    "Scope": "catalog.read"
  }
```

- [ ] **Step 4: Program.cs — identity HttpClient + provider kaydı**

`src/AgentOrchestrator/Program.cs`'te `builder.Services.AddHttpContextAccessor();` altına:
```csharp
builder.Services.AddHttpClient("identity");
builder.Services.AddSingleton<IClientCredentialsTokenProvider, ClientCredentialsTokenProvider>();
```

- [ ] **Step 5: Build + Commit**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/AgentOrchestrator/AgentOrchestrator.csproj`
Expected: Build succeeded, 0 Error.
```bash
git add src/AgentOrchestrator/AgentOrchestrator.csproj src/AgentOrchestrator/ClientCredentialsTokenProvider.cs src/AgentOrchestrator/appsettings.json src/AgentOrchestrator/Program.cs
git commit -m "feat(orchestrator): add m2m client_credentials token provider" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 6: Orchestrator — per-invocation token handler + McpToolProvider sadeleştir

**Files:**
- Create: `src/AgentOrchestrator/TokenInjectingHandler.cs`
- Modify: `src/AgentOrchestrator/McpToolProvider.cs`
- Modify: `src/AgentOrchestrator/Program.cs`

**Interfaces:**
- Consumes: `IClientCredentialsTokenProvider` (Task 5), `IHttpContextAccessor`.
- Produces: MCP `HttpClient`'a takılı `TokenInjectingHandler`.

- [ ] **Step 1: TokenInjectingHandler.cs oluştur**

```csharp
using System.Net.Http.Headers;

namespace AgentOrchestrator;

// MCP'ye giden her istege token iliştirir: o anki isteğin kullanici token'i varsa onu
// (per-user), yoksa m2m client_credentials token'i (anonim/acilis kesfi). Yetki downstream
// handler middleware'inde (per-tool scope) kontrol edilir.
public sealed class TokenInjectingHandler(
    IHttpContextAccessor accessor,
    IClientCredentialsTokenProvider clientCredentials) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = null;

        var incoming = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(incoming))
        {
            request.Headers.TryAddWithoutValidation("Authorization", incoming);
        }
        else
        {
            var token = await clientCredentials.GetTokenAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

- [ ] **Step 2: McpToolProvider.cs'ten token okuma mantığını çıkar**

`RequestScopedMcpToolProvider` sınıfını şununla değiştir (artık `IHttpContextAccessor` yok; Authorization handler'da):

```csharp
public sealed class RequestScopedMcpToolProvider(
    HttpClient httpClient,
    ILogger<RequestScopedMcpToolProvider> logger) : IMcpToolProvider
{
    public async Task<IList<McpClientTool>> GetToolsAsync(string serverName, string url, CancellationToken ct = default)
    {
        try
        {
            var client = await McpClient.CreateAsync(
                new HttpClientTransport(
                    new HttpClientTransportOptions { Name = serverName, Endpoint = new Uri(url) },
                    httpClient,
                    NullLoggerFactory.Instance,
                    ownsHttpClient: false),
                cancellationToken: ct);

            return await client.ListToolsAsync(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP '{Server}' tool kesfi basarisiz; bu istek icin atlandi.", serverName);
            return [];
        }
    }
}
```
(Interface `IMcpToolProvider`, `McpToolProviderExtensions` aynı kalır. `using Microsoft.AspNetCore.Http;` gereksizse kaldırılabilir.)

- [ ] **Step 3: Program.cs — handler'ı MCP HttpClient'ına bağla**

Mevcut:
```csharp
builder.Services.AddHttpClient<IMcpToolProvider, RequestScopedMcpToolProvider>();
```
Yeni:
```csharp
builder.Services.AddTransient<TokenInjectingHandler>();
builder.Services.AddHttpClient<IMcpToolProvider, RequestScopedMcpToolProvider>()
    .AddHttpMessageHandler<TokenInjectingHandler>();
```

- [ ] **Step 4: Build + Commit**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/AgentOrchestrator/AgentOrchestrator.csproj`
Expected: Build succeeded, 0 Error.
```bash
git add src/AgentOrchestrator/TokenInjectingHandler.cs src/AgentOrchestrator/McpToolProvider.cs src/AgentOrchestrator/Program.cs
git commit -m "feat(orchestrator): inject token per MCP invocation via DelegatingHandler" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 7: Runtime doğrulama (çalışan Aspire stack)

**Files:** yok (curl/manuel). Orchestrator portunu bul: `for p in $(lsof -nP -iTCP -sTCP:LISTEN | awk 'NR>1{print $9}' | sed -E 's/.*:([0-9]+)$/\1/' | sort -un | grep -E '^5[0-9]{3}$'); do [ "$(curl -s -o /dev/null -w %{http_code} -m 3 http://localhost:$p/public/v1/responses)" = "405" ] && echo $p; done`

- [ ] **D1 — Anonim arama gerçek veri:**
```bash
curl -sS -N -m 45 -X POST "http://localhost:<ORCH>/public/v1/responses" \
  -H "Content-Type: application/json" -H "Accept: text/event-stream" \
  -d '{"model":"public","input":"Nike marka urunleri ara","stream":true}' | grep -E "search_products|output_text.delta|response.failed"
```
Expected: `search_products` çağrısı + gerçek ürün içeren delta'lar; `response.failed` YOK.

- [ ] **D2 — Anonim yazma reddedilir:** Sohbette "yeni ürün ekle" → `create_product` → m2m'de `catalog.write` yok → `ScopeAuthorizationMiddleware` `UnauthorizedAccessException` → ürün oluşmaz.

- [ ] **D3 — Login per-user:** Widget'ta giriş yap → "sepetimi göster"/"sepete ekle" → `get_basket`/`add_to_cart` kullanıcı token'ıyla çalışır.

- [ ] **D4 — Keşif token'la geçer:** Açılışta orchestrator m2m token'la tool'ları keşfeder; dashboard'da MCP `RequireAuthorization` 401 warning'i YOK; agent tool'lara sahip.

- [ ] **D5 — Streaming + çok turlu hafıza** bozulmadı.

- [ ] **D6 — Handler lifecycle:** Birkaç dakika sonra tekrar arama; typed-client/handler rotasyonu sorun çıkarmıyor. Sorunda: named client + `ConfigurePrimaryHttpMessageHandler`.

---

## Self-Review Notu

- **Spec kapsamı:** 4a (attribute) → Task 2; 4b (middleware) → Task 2; 4c (anotasyon + kayıt) → Task 3-4; 4d (orchestrator token) → Task 5-6; 4e (defense-in-depth korunur) → Task 1 (revert) + hiçbir auth kaldırılmadı; doğrulama → Task 7.
- **Tip tutarlılığı:** `RequiredScopeAttribute.Scope` ↔ middleware okuması; `HasScope` (mevcut) ↔ middleware; `IClientCredentialsTokenProvider.GetTokenAsync` ↔ handler; `RequestScopedMcpToolProvider` adı korunur.
- **İşaretli runtime belirsizlikleri:** (a) Wolverine `Before(Envelope,...)` + `AddMiddleware(Type)` → Task 2/3 build gate'i doğrular (alternatif: `AddMiddleware<T>()`); (b) typed-client handler rotasyonu → D6; (c) `UnauthorizedAccessException`'ın REST'te 403'e map'i → REST'te zaten `RequireAuthorization` defense-in-depth var, middleware throw'u pratikte yalnız MCP yolunda devreye girer.
---

# rev3 — Token'ı WebApp sağlıyor (orchestrator m2m kaldırılır)

**Değişiklik:** WebApp BFF her zaman token gönderdiği için (anonim: ecommerce.bff client_credentials; login: user) orchestrator'ın kendi m2m provider'ına gerek yok. MCP auth tek noktada (handler middleware). Transport/gateway yalnız yönlendirir. Bu bölüm **Task 5-6'yı supersede eder** ve transport'u açar.

## Task R1: Gateway `/mcp/*` anonim — commit
- [ ] Kullanıcının `catalog-mcp-route`/`basket-mcp-route`'tan `AuthorizationPolicy` kaldıran düzenlemesini commit'le.
  `git add src/services/gateway/Gateway/appsettings.Development.json && git commit -m "feat(gateway): make /mcp routes anonymous (gateway cannot validate forwarded token audience)"`

## Task R2: Catalog + Basket MCP transport'unu aç
- [ ] `Catalog.Api/Program.cs`: `app.MapMcp("/mcp").RequireAuthorization();` → `app.MapMcp("/mcp");` (yetki handler middleware'de; açılış keşfi token'sız çalışsın).
- [ ] `Basket.Api/Program.cs`: aynı.
- [ ] Build (catalog, basket) 0 error. Commit.

## Task R3: Orchestrator m2m provider'ı geri al
- [ ] `ClientCredentialsTokenProvider.cs` **sil**.
- [ ] `Program.cs`: `AddHttpClient("identity")` + `AddSingleton<IClientCredentialsTokenProvider,...>()` satırlarını kaldır.
- [ ] `appsettings.json`: `IdentityServer` bölümünü kaldır.
- [ ] `AgentOrchestrator.csproj`: `Duende.IdentityModel` paketini kaldır.

## Task R4: TokenInjectingHandler'ı sadeleştir
- [ ] `TokenInjectingHandler`: `IClientCredentialsTokenProvider` bağımlılığını ve m2m dalını kaldır; yalnız gelen `Authorization`'ı forward et:
```csharp
public sealed class TokenInjectingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("Authorization");
        var incoming = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(incoming))
            request.Headers.TryAddWithoutValidation("Authorization", incoming);
        return await base.SendAsync(request, cancellationToken);
    }
}
```
- [ ] Build (orchestrator) 0 error. Commit (R3+R4 birlikte).

## Task R5: Runtime doğrulama
- [ ] Önceki Task 7 (D1-D5) — artık anonim arama WebApp m2m token'ıyla, açılış keşfi token'sız çalışır.
