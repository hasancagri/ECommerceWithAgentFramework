# Orchestrator MCP Token-at-Invocation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agent'lar MCP tool'larını gerçekten kullanabilsin; tool keşfi auth'suz olsun, token çağrı anında iliştirilsin, yetki downstream'de per-tool scope ile kontrol edilsin.

**Architecture:** MCP server'lar transport kapısını (`MapMcp().RequireAuthorization()`) bırakır ve her tool kendi scope'unu kontrol eder. Gateway `/mcp/*` route'ları anonime açılır. Orchestrator, MCP `HttpClient`'ına bir `DelegatingHandler` takıp her çağrıda token enjekte eder (isteğin kullanıcı token'ı varsa o, yoksa m2m client_credentials). Agent'lar Singleton kalır; tool'lar açılışta bir kez keşfedilir.

**Tech Stack:** ASP.NET Core (net10.0), ModelContextProtocol (MCP server/client), Duende.IdentityModel.Client (m2m token), Microsoft.Extensions.AI, .NET Aspire.

**Spec:** `docs/superpowers/specs/2026-06-29-orchestrator-mcp-token-at-invocation-design.md`

## Global Constraints

- Hedef framework: `net10.0` (tüm projeler).
- **Test/TDD bu planın dışında** — kullanıcı kararı ("çalışsın, refactor sonra"). Her görevin kapısı `dotnet build` (0 error). Uçtan uca doğrulama Task 7'de (çalışan Aspire stack ile; identity `http://localhost:5001`).
- Hardcode port yok; downstream'ler service discovery, identity `http://localhost:5001` (dev).
- Güvenlik: keşif anonim (sadece metadata); m2m yalnız read scope'ları → anonim read-only, login per-user.
- `scope` claim semantiği mevcut JWT auth ile aynı (`MapInboundClaims=false`, policy'ler `RequireClaim("scope", x)`).

**Build komutları (mutlak yol):**
- Common: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/Common/Common.csproj`
- Catalog: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/services/catalog/Catalog.Api/Catalog.Api.csproj`
- Basket: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/services/basket/Basket.Api/Basket.Api.csproj`
- Gateway: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/services/gateway/Gateway/Gateway.csproj`
- Orchestrator: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/AgentOrchestrator/AgentOrchestrator.csproj`

---

## File Structure

| Dosya | Sorumluluk | İşlem |
|------|-----------|------|
| `src/Common/Utils/Authorization/ScopeAuthorizationExtensions.cs` | `ClaimsPrincipal.HasScope(scope)` yardımcı | Create |
| `src/services/catalog/Catalog.Api/Domains/Products/ProductMcpTools.cs` | Her tool'a scope guard + `IHttpContextAccessor` param | Modify |
| `src/services/catalog/Catalog.Api/Program.cs` | `MapMcp` transport kapısını kaldır | Modify |
| `src/services/basket/Basket.Api/Domains/Baskets/BasketMcpTools.cs` | Her tool'a scope guard | Modify |
| `src/services/basket/Basket.Api/Program.cs` | `MapMcp` transport kapısını kaldır | Modify |
| `src/services/gateway/Gateway/appsettings.Development.json` | `*-mcp-route` AuthorizationPolicy kaldır | Modify |
| `src/AgentOrchestrator/ClientCredentialsTokenProvider.cs` | m2m token edin + cache | Create |
| `src/AgentOrchestrator/TokenInjectingHandler.cs` | Per-invocation token enjeksiyonu | Create |
| `src/AgentOrchestrator/McpToolProvider.cs` | Token okuma çıkar (handler'a taşındı) | Modify |
| `src/AgentOrchestrator/AgentOrchestrator.csproj` | Duende.IdentityModel paketi | Modify |
| `src/AgentOrchestrator/appsettings.json` | IdentityServer (m2m) config | Modify |
| `src/AgentOrchestrator/Program.cs` | Handler + token provider + identity HttpClient kaydı | Modify |

---

## Task 1: Common — scope kontrol yardımcısı

**Files:**
- Create: `src/Common/Utils/Authorization/ScopeAuthorizationExtensions.cs`

**Interfaces:**
- Produces: `Common.ScopeAuthorizationExtensions.HasScope(this ClaimsPrincipal? user, string scope) -> bool`. MCP tool guard'ları bunu kullanır. Sadece `System.Security.Claims`'e bağlı (Common'a ASP.NET bağımlılığı eklemez).

- [ ] **Step 1: Yardımcıyı oluştur**

```csharp
using System.Security.Claims;

namespace Common;

// MCP tool'lari icinde per-tool yetki kontrolu: forward edilen token'daki "scope" claim'i.
// JWT auth MapInboundClaims=false ile claim tipini "scope" birakir; policy'ler de
// RequireClaim("scope", x) kullanir => ayni semantik. Hem coklu "scope" claim'i hem de
// bosluklu tek "scope" claim'i tolere edilir.
public static class ScopeAuthorizationExtensions
{
    public static bool HasScope(this ClaimsPrincipal? user, string scope)
    {
        if (user is null)
            return false;

        foreach (var claim in user.FindAll("scope"))
            if (claim.Value == scope || claim.Value.Split(' ').Contains(scope))
                return true;

        return false;
    }
}
```

- [ ] **Step 2: Build doğrulaması**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/Common/Common.csproj`
Expected: Build succeeded, 0 Error.

- [ ] **Step 3: Commit**

```bash
git add src/Common/Utils/Authorization/ScopeAuthorizationExtensions.cs
git commit -m "feat(common): add ClaimsPrincipal.HasScope helper for per-tool MCP authz" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 2: Catalog MCP — per-tool scope + transport kapısını aç

**Files:**
- Modify: `src/services/catalog/Catalog.Api/Domains/Products/ProductMcpTools.cs`
- Modify: `src/services/catalog/Catalog.Api/Program.cs:101`

**Interfaces:**
- Consumes: `ClaimsPrincipal.HasScope` (Task 1), `Common.Utils.Constants.AuthorizationScopes`, `Common.MessageItem`, `Common.FeatureObjectResultModel<T>`.
- Produces: read tool'lar `catalog.read`, write tool'lar `catalog.write` ister; yoksa `FeatureObjectResultModel<T>.Error(...)` (Code `unauthorized_scope`).

- [ ] **Step 1: ProductMcpTools.cs'i guard'larla değiştir**

Dosyanın TAMAMINI şununla değiştir (her tool'a `IHttpContextAccessor http` param + scope guard eklendi):

```csharp
using System.ComponentModel;
using Catalog.Api.Domains.Products.Features.Commands;
using Catalog.Api.Domains.Products.Features.Queries;
using Common;
using Common.Utils.Constants;
using ModelContextProtocol.Server;
using Shared.Enums;

namespace Catalog.Api.Domains.Products;

[McpServerToolType]
public static class GetProductMcpTool
{
    [McpServerTool(Name = "get_product")]
    [Description("Verilen Id'ye sahip urunu doner.")]
    public static Task<FeatureObjectResultModel<GetProductById.ProductResponse>> GetProductAsync(
        [Description("Urunun Id'si")] Guid id,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.CatalogRead) != true)
            return Task.FromResult(FeatureObjectResultModel<GetProductById.ProductResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        return bus.InvokeAsync<FeatureObjectResultModel<GetProductById.ProductResponse>>(
            new GetProductById.GetProductByIdQuery(id), ct);
    }
}

[McpServerToolType]
public static class GetProductByNameMcpTool
{
    [McpServerTool(Name = "search_products")]
    [Description("Urunleri isme gore arar (kismi eslesme, buyuk/kucuk harf duyarsiz). Coklu sonuc donebilir.")]
    public static Task<FeatureObjectResultModel<List<GetProductById.ProductResponse>>> SearchProductsAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        [Description("Donecek azami sonuc sayisi (1-20, varsayilan 5)")] int? limit,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.CatalogRead) != true)
            return Task.FromResult(FeatureObjectResultModel<List<GetProductById.ProductResponse>>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        return bus.InvokeAsync<FeatureObjectResultModel<List<GetProductById.ProductResponse>>>(
            new GetProductByName.GetProductByNameQuery(name, limit ?? 5), ct);
    }
}

[McpServerToolType]
public static class CreateProductMcpTool
{
    [McpServerTool(Name = "create_product")]
    [Description("Kataloga yeni bir urun ekler.")]
    public static Task<FeatureObjectResultModel<CreateProduct.CreateProductResponse>> CreateProductAsync(
        [Description("Urun adi")] string name,
        [Description("Urun aciklamasi")] string description,
        [Description("Fiyat (ondalikli, orn. 199.90)")] decimal price,
        [Description("Stok kodu (SKU)")] string sku,
        [Description("Marka: Apple=1, Samsung=2, Sony=3, Nike=4, Adidas=5, Lenovo=6, Dell=7")] BrandType brand,
        [Description("Urun gorsel URL'si (opsiyonel)")] string? imageUrl,
        [Description("Baslangic stok adedi")] int initialStock,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.CatalogWrite) != true)
            return Task.FromResult(FeatureObjectResultModel<CreateProduct.CreateProductResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        return bus.InvokeAsync<FeatureObjectResultModel<CreateProduct.CreateProductResponse>>(
            new CreateProduct.CreateProductCommand(name, description, price, sku, brand, imageUrl, initialStock), ct);
    }
}

[McpServerToolType]
public static class UpdateProductMcpTool
{
    [McpServerTool(Name = "update_product")]
    [Description("Mevcut bir urunu gunceller.")]
    public static Task<FeatureObjectResultModel<UpdateProduct.UpdateProductResponse>> UpdateProductAsync(
        [Description("Guncellenecek urunun Id'si")] Guid id,
        [Description("Urun adi")] string name,
        [Description("Urun aciklamasi")] string description,
        [Description("Fiyat (ondalikli, orn. 199.90)")] decimal price,
        [Description("Stok kodu (SKU)")] string sku,
        [Description("Marka: Apple=1, Samsung=2, Sony=3, Nike=4, Adidas=5, Lenovo=6, Dell=7")] BrandType brand,
        [Description("Urun gorsel URL'si (opsiyonel)")] string? imageUrl,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.CatalogWrite) != true)
            return Task.FromResult(FeatureObjectResultModel<UpdateProduct.UpdateProductResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        return bus.InvokeAsync<FeatureObjectResultModel<UpdateProduct.UpdateProductResponse>>(
            new UpdateProduct.UpdateProductCommand(id, name, description, price, sku, brand, imageUrl), ct);
    }
}

[McpServerToolType]
public static class DeleteProductMcpTool
{
    [McpServerTool(Name = "delete_product")]
    [Description("Verilen Id'ye sahip urunu siler.")]
    public static Task<FeatureObjectResultModel<DeleteProduct.DeleteProductResponse>> DeleteProductAsync(
        [Description("Silinecek urunun Id'si")] Guid id,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.CatalogWrite) != true)
            return Task.FromResult(FeatureObjectResultModel<DeleteProduct.DeleteProductResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        return bus.InvokeAsync<FeatureObjectResultModel<DeleteProduct.DeleteProductResponse>>(
            new DeleteProduct.DeleteProductCommand(id), ct);
    }
}
```

- [ ] **Step 2: Program.cs — transport kapısını kaldır**

`src/services/catalog/Catalog.Api/Program.cs:101`'i değiştir:

Mevcut:
```csharp
app.MapMcp("/mcp").RequireAuthorization();
```
Yeni (keşif anonim; yetki tool icinde):
```csharp
// Transport kapisi YOK: tool kesfi (ListTools) anonim. Yetki her tool icinde HasScope ile.
app.MapMcp("/mcp");
```

- [ ] **Step 3: Build doğrulaması**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/services/catalog/Catalog.Api/Catalog.Api.csproj`
Expected: Build succeeded, 0 Error.

- [ ] **Step 4: Commit**

```bash
git add src/services/catalog/Catalog.Api/Domains/Products/ProductMcpTools.cs src/services/catalog/Catalog.Api/Program.cs
git commit -m "feat(catalog): per-tool scope authz on MCP tools, open transport for discovery" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 3: Basket MCP — per-tool scope + transport kapısını aç

**Files:**
- Modify: `src/services/basket/Basket.Api/Domains/Baskets/BasketMcpTools.cs`
- Modify: `src/services/basket/Basket.Api/Program.cs:74`

**Interfaces:**
- Consumes: `ClaimsPrincipal.HasScope` (Task 1), `Common.Utils.Constants.AuthorizationScopes`, `Common.MessageItem`.
- Produces: `get_basket` → `basket.read`; `add_to_cart`/`remove_basket_item`/`apply_discount_coupon`/`remove_discount_coupon` → `basket.write`. Guard, `CurrentUser.Load`'dan ÖNCE çalışır (anonimde NRE'yi önler).

- [ ] **Step 1: BasketMcpTools.cs'i guard'larla değiştir**

Dosyanın TAMAMINI şununla değiştir:

```csharp
using System.ComponentModel;
using Common;
using Common.Auths;
using Common.Utils.Constants;
using ModelContextProtocol.Server;

namespace Basket.Api.Domains.Baskets;

// Basket operasyonlari MCP tool'lari olarak. UserId parametre DEGIL; forward edilen
// token'dan (CurrentUser.Load) alinir. Yetki guard'i CurrentUser.Load'dan ONCE.

[McpServerToolType]
public static class AddToCartMcpTool
{
    [McpServerTool(Name = "add_to_cart")]
    [Description("Giris yapmis kullanicinin sepetine bir urun ekler.")]
    public static Task<FeatureObjectResultModel<AddBasketItem.AddBasketItemResponse>> AddToCartAsync(
        [Description("Sepete eklenecek urunun Id'si")] Guid productId,
        [Description("Urun adi")] string productName,
        [Description("Urun fiyati (ondalikli, orn. 199.90)")] decimal price,
        [Description("Urun gorsel URL'si (opsiyonel)")] string? imageUrl,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.BasketWrite) != true)
            return Task.FromResult(FeatureObjectResultModel<AddBasketItem.AddBasketItemResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AddBasketItem.AddBasketItemResponse>>(
            new AddBasketItem.AddBasketItemCommand(userId, productId, productName, price, imageUrl), ct);
    }
}

[McpServerToolType]
public static class GetBasketMcpTool
{
    [McpServerTool(Name = "get_basket")]
    [Description("Giris yapmis kullanicinin sepetini (urunler, toplam fiyat, indirim) doner.")]
    public static Task<FeatureObjectResultModel<GetBasket.GetBasketResponse>> GetBasketAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.BasketRead) != true)
            return Task.FromResult(FeatureObjectResultModel<GetBasket.GetBasketResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<GetBasket.GetBasketResponse>>(
            new GetBasket.GetBasketQuery(userId), ct);
    }
}

[McpServerToolType]
public static class RemoveBasketItemMcpTool
{
    [McpServerTool(Name = "remove_basket_item")]
    [Description("Sepetten verilen Id'ye sahip urunu cikarir.")]
    public static Task<FeatureObjectResultModel<DeleteBasketItem.DeleteBasketItemResponse>> RemoveBasketItemAsync(
        [Description("Sepetten cikarilacak urunun (sepet item) Id'si")] Guid itemId,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.BasketWrite) != true)
            return Task.FromResult(FeatureObjectResultModel<DeleteBasketItem.DeleteBasketItemResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<DeleteBasketItem.DeleteBasketItemResponse>>(
            new DeleteBasketItem.DeleteBasketItemCommand(userId, itemId), ct);
    }
}

[McpServerToolType]
public static class ApplyDiscountCouponMcpTool
{
    [McpServerTool(Name = "apply_discount_coupon")]
    [Description("Sepete bir indirim kuponu uygular.")]
    public static Task<FeatureObjectResultModel<ApplyDiscountCoupon.ApplyDiscountCouponResponse>> ApplyDiscountCouponAsync(
        [Description("Kupon kodu")] string coupon,
        [Description("Indirim orani (0-1 arasi, orn. 0.15 = %15)")] float discountRate,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.BasketWrite) != true)
            return Task.FromResult(FeatureObjectResultModel<ApplyDiscountCoupon.ApplyDiscountCouponResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<ApplyDiscountCoupon.ApplyDiscountCouponResponse>>(
            new ApplyDiscountCoupon.ApplyDiscountCouponCommand(userId, coupon, discountRate), ct);
    }
}

[McpServerToolType]
public static class RemoveDiscountCouponMcpTool
{
    [McpServerTool(Name = "remove_discount_coupon")]
    [Description("Sepete uygulanmis indirim kuponunu kaldirir.")]
    public static Task<FeatureObjectResultModel<RemoveDiscountCoupon.RemoveDiscountCouponResponse>> RemoveDiscountCouponAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.BasketWrite) != true)
            return Task.FromResult(FeatureObjectResultModel<RemoveDiscountCoupon.RemoveDiscountCouponResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<RemoveDiscountCoupon.RemoveDiscountCouponResponse>>(
            new RemoveDiscountCoupon.RemoveDiscountCouponCommand(userId), ct);
    }
}
```

- [ ] **Step 2: Program.cs — transport kapısını kaldır**

`src/services/basket/Basket.Api/Program.cs:74`'ü değiştir:

Mevcut:
```csharp
app.MapMcp("/mcp").RequireAuthorization();
```
Yeni:
```csharp
// Transport kapisi YOK: tool kesfi anonim. Yetki her tool icinde HasScope ile.
app.MapMcp("/mcp");
```

- [ ] **Step 3: Build doğrulaması**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/services/basket/Basket.Api/Basket.Api.csproj`
Expected: Build succeeded, 0 Error.

- [ ] **Step 4: Commit**

```bash
git add src/services/basket/Basket.Api/Domains/Baskets/BasketMcpTools.cs src/services/basket/Basket.Api/Program.cs
git commit -m "feat(basket): per-tool scope authz on MCP tools, open transport for discovery" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 4: Gateway — `/mcp/*` route'larını anonim yap

**Files:**
- Modify: `src/services/gateway/Gateway/appsettings.Development.json` (catalog-mcp-route, basket-mcp-route)

**Interfaces:**
- Produces: `/mcp/catalog/*` ve `/mcp/basket/*` route'ları auth policy'siz; gateway token'ı (varsa) downstream'e geçirir, yoksa anonim geçirir.

- [ ] **Step 1: `catalog-mcp-route`'tan AuthorizationPolicy kaldır**

`catalog-mcp-route` bloğundaki `Transforms` satırının sonundaki virgülü kaldır ve `"AuthorizationPolicy": "ClientCredential"` satırını sil. Blok şöyle olmalı:

```json
      "catalog-mcp-route": {
        "ClusterId": "catalog.cluster",
        "Match": {
          "Path": "/mcp/catalog/{**catch-all}"
        },
        "Transforms": [ { "PathPattern": "/mcp/{**catch-all}" } ]
      },
```

- [ ] **Step 2: `basket-mcp-route`'tan AuthorizationPolicy kaldır**

Aynı şekilde blok:

```json
      "basket-mcp-route": {
        "ClusterId": "basket.cluster",
        "Match": {
          "Path": "/mcp/basket/{**catch-all}"
        },
        "Transforms": [ { "PathPattern": "/mcp/{**catch-all}" } ]
      },
```

- [ ] **Step 3: Build doğrulaması (config geçerliliği)**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/services/gateway/Gateway/Gateway.csproj`
Expected: Build succeeded, 0 Error. (JSON sözdizimi: virgül/parantez bozulmadı.)

- [ ] **Step 4: Commit**

```bash
git add src/services/gateway/Gateway/appsettings.Development.json
git commit -m "feat(gateway): make /mcp routes anonymous (authz moved to MCP tools)" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 5: Orchestrator — m2m client_credentials token provider

**Files:**
- Modify: `src/AgentOrchestrator/AgentOrchestrator.csproj`
- Create: `src/AgentOrchestrator/ClientCredentialsTokenProvider.cs`
- Modify: `src/AgentOrchestrator/appsettings.json`
- Modify: `src/AgentOrchestrator/Program.cs` (identity HttpClient + provider kaydı)

**Interfaces:**
- Consumes: config `IdentityServer:Authority|ClientId|ClientSecret|Scope`; named HttpClient `"identity"`.
- Produces: `IClientCredentialsTokenProvider.GetTokenAsync(CancellationToken) -> Task<string?>` (Task 6 kullanır). DI: Singleton.

- [ ] **Step 1: Duende.IdentityModel paketini ekle**

`src/AgentOrchestrator/AgentOrchestrator.csproj` içindeki `<ItemGroup>` paket bloğuna ekle (sürüm merkezi yönetimden gelir):

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

`src/AgentOrchestrator/appsettings.json` kök objesine şu bölümü ekle (mevcut JSON'a virgülle, dev secret kaynakta zaten mevcut):

```json
  "IdentityServer": {
    "Authority": "http://localhost:5001",
    "ClientId": "m2m.client",
    "ClientSecret": "dev-secret",
    "Scope": "catalog.read"
  }
```

- [ ] **Step 4: Program.cs — identity HttpClient + provider kaydı**

`src/AgentOrchestrator/Program.cs` içinde, `builder.Services.AddHttpContextAccessor();` satırının altına ekle:

```csharp
// m2m token edinmek icin IdentityServer'a giden adsiz client.
builder.Services.AddHttpClient("identity");
builder.Services.AddSingleton<IClientCredentialsTokenProvider, ClientCredentialsTokenProvider>();
```

- [ ] **Step 5: Build doğrulaması**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/AgentOrchestrator/AgentOrchestrator.csproj`
Expected: Build succeeded, 0 Error.

- [ ] **Step 6: Commit**

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
- Produces: MCP `HttpClient`'a takılı `TokenInjectingHandler`; her giden MCP isteğine Authorization ekler. `McpToolProvider` artık token okumaz.

- [ ] **Step 1: TokenInjectingHandler.cs oluştur**

```csharp
using System.Net.Http.Headers;

namespace AgentOrchestrator;

// MCP'ye giden her istege token iliştirir: o anki isteğin kullanici token'i varsa onu
// (per-user), yoksa m2m client_credentials token'i (anonim). Yetki downstream MCP
// server'da per-tool scope ile kontrol edilir. Singleton agent + per-invocation token.
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

`src/AgentOrchestrator/McpToolProvider.cs` içindeki `RequestScopedMcpToolProvider` sınıfını şununla değiştir (artık `IHttpContextAccessor` yok; Authorization handler'da ekleniyor):

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

(Dosyanın başındaki `IMcpToolProvider` arayüzü, `McpToolProviderExtensions` ve `using`'ler aynı kalır; yalnız `using Microsoft.AspNetCore.Http;` artık gereksizse kaldırılabilir — derleme uyarısı vermez, bırakmak da zararsız.)

- [ ] **Step 3: Program.cs — handler'ı MCP HttpClient'ına bağla**

`src/AgentOrchestrator/Program.cs` içinde mevcut satırı değiştir:

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

- [ ] **Step 4: Build doğrulaması**

Run: `dotnet build /Users/macbook/Desktop/ECommerceWithAgentFramework/src/AgentOrchestrator/AgentOrchestrator.csproj`
Expected: Build succeeded, 0 Error.

- [ ] **Step 5: Commit**

```bash
git add src/AgentOrchestrator/TokenInjectingHandler.cs src/AgentOrchestrator/McpToolProvider.cs src/AgentOrchestrator/Program.cs
git commit -m "feat(orchestrator): inject token per MCP invocation via DelegatingHandler" -m "Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
```

---

## Task 7: Runtime doğrulama (çalışan Aspire stack)

**Files:** yok (manuel/curl doğrulama). Kod üretmez.

**Önkoşul:** AppHost çalışıyor; identity `http://localhost:5001` discovery sunuyor; OpenAI key user-secrets'te. Orchestrator portunu bul: `for p in $(lsof -nP -iTCP -sTCP:LISTEN | awk 'NR>1{print $9}' | sed -E 's/.*:([0-9]+)$/\1/' | sort -un); do [ "$(curl -s -o /dev/null -w %{http_code} -m 3 http://localhost:$p/public/v1/responses)" = "405" ] && echo $p; done`

- [ ] **Doğrulama 1 — Anonim arama gerçek veri döner:**

```bash
curl -sS -N -m 45 -X POST "http://localhost:<ORCH>/public/v1/responses" \
  -H "Content-Type: application/json" -H "Accept: text/event-stream" \
  -d '{"model":"public","input":"Nike marka urunleri ara","stream":true}' | grep -E "search_products|function_call|output_text.delta|response.failed"
```
Expected: `search_products` tool çağrısı ve gerçek ürün verisi içeren `output_text.delta`; `response.failed` YOK; "lütfen bekleyin"de takılmaz.

- [ ] **Doğrulama 2 — Anonim yazma reddedilir:** Anonim agent `create_product` denerse `unauthorized_scope` (m2m'de `catalog.write` yok) → ürün oluşmaz. (Sohbette "yeni ürün ekle" deyip gözlemle.)

- [ ] **Doğrulama 3 — Login per-user:** Widget'ta giriş yapıp "sepetimi göster" / "şunu sepete ekle" → `get_basket`/`add_to_cart` kullanıcı token'ıyla çalışır; gerçek sepet döner.

- [ ] **Doğrulama 4 — Keşif anonim:** Orchestrator açılışta tool'ları token'sız toplar (dashboard'da MCP tool kesfi warning'i YOK; agent tool'lara sahip).

- [ ] **Doğrulama 5 — Streaming + çok turlu hafıza** bozulmadı (ikinci mesaj bağlamı hatırlar).

- [ ] **Doğrulama 6 — Handler lifecycle:** Uzun oturumda (birkaç dakika) tekrar arama yap; MCP `HttpClient`/handler rotasyonu sorun çıkarmıyor (tool çağrıları hâlâ çalışıyor). Sorun çıkarsa: handler'ı typed-client yerine ayrı bir named client + `ConfigurePrimaryHttpMessageHandler` ile kur.

---

## Self-Review Notu (yazım anında)

- **Spec kapsamı:** 4a (per-tool scope) → Task 2-3; transport kapısı kaldırma → Task 2-3; 4b (gateway anonim) → Task 4; 4c (handler + token provider + McpToolProvider sadeleştirme) → Task 5-6; doğrulama → Task 7. Hepsi karşılandı.
- **Tip tutarlılığı:** `HasScope(this ClaimsPrincipal?, string)` (Task 1) ↔ `http.HttpContext?.User.HasScope(...)` (Task 2-3); `IClientCredentialsTokenProvider.GetTokenAsync` (Task 5) ↔ handler kullanımı (Task 6); `RequestScopedMcpToolProvider` adı korunur.
- **Runtime belirsizlikleri (işaretli):** (a) MCP tool param'ına `IHttpContextAccessor` DI enjeksiyonu — basket tool'ları zaten yapıyor, doğrulandı; (b) typed-client + uzun yaşayan MCP client handler rotasyonu → Doğrulama 6'da izlenir, gerekirse named client'a geçilir; (c) `scope` claim formatı (çoklu vs bosluklu) → `HasScope` ikisini de tolere eder.