# ChatAgent MCP Tool Allowlist Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ChatAgent'ın public/assistant agent'larının bir MCP server'ın tüm tool'larını değil, yalnızca her agent'a açıkça izin verilen tool'ları toplamasını sağlamak (zorunlu allowlist).

**Architecture:** `CollectTools`/`IMcpToolProvider.GetToolsAsync` server-granülerden tool-adı granülerine çıkarılır; `ListTools` sonucu provider içinde (logger sahibi) allowlist'e göre filtrelenir. Sabitler `ConstValues.cs`'e eklenir, iki agent factory'si allowlist geçirir. Tek atomik değişiklik: imza değişince tüm çağrı yerleri birlikte derlenir.

**Tech Stack:** .NET 10, Microsoft Agent Framework (Microsoft.Agents.AI), ModelContextProtocol.Client, `dotnet` slnx solution.

## Global Constraints

- Test projesi yok; tek doğrulama `dotnet build`.
- Allowlist ZORUNLU: her server girişi izin verilen tool adlarını belirtir; implicit "hepsini al" yoktur. Bilinmeyen/yeni tool varsayılan olarak dışarıda (fail-safe).
- Bu bir tool-sunum katmanıdır, yetki sınırı değil: servislerdeki `[RequiredScope]` middleware'i KALDIRILMAZ.
- Tool-adı sabitlerinin değerleri servislerdeki `[McpServerTool(Name = ...)]` ile birebir aynı olmalı.
- Kod yorumları Türkçe (proje konvansiyonu).
- Merkezi NuGet sürümleri; csproj'a `Version=` eklenmez (bu planda paket değişikliği yok).

---

### Task 1: Tool allowlist filtresi (sabitler + provider + agent wiring)

**Files:**
- Modify: `src/ChatAgent/ConstValues.cs` (yeni `CatalogTools` / `BasketTools` sabit sınıfları)
- Modify: `src/ChatAgent/McpToolProvider.cs` (imza + filtreleme + uyarı)
- Modify: `src/ChatAgent/Program.cs` (iki agent factory'sinde allowlist)

**Interfaces:**
- Produces: `IMcpToolProvider.GetToolsAsync(string serverName, string url, IReadOnlyCollection<string> allowedTools, CancellationToken ct = default)`; `CollectTools(this IMcpToolProvider, params (string Name, string Url, string[] AllowedTools)[] servers)`; `CatalogTools.{GetProduct,SearchProducts,DeleteProduct}`, `BasketTools.{AddToCart,GetBasket,RemoveBasketItem,ApplyDiscountCoupon,RemoveDiscountCoupon}`.

- [ ] **Step 1: Tool-adı sabitlerini ekle**

`src/ChatAgent/ConstValues.cs` içindeki `McpServers` sınıfının hemen ALTINA (namespace içinde) ekle:

```csharp
// MCP tool adlari. Servislerdeki [McpServerTool(Name = ...)] ile birebir ayni degerler.
// Allowlist'lerde kullanilir; magic string tekrarini onler.
public static class CatalogTools
{
    public const string GetProduct = "get_product";
    public const string SearchProducts = "search_products";
    public const string DeleteProduct = "delete_product";
}

public static class BasketTools
{
    public const string AddToCart = "add_to_cart";
    public const string GetBasket = "get_basket";
    public const string RemoveBasketItem = "remove_basket_item";
    public const string ApplyDiscountCoupon = "apply_discount_coupon";
    public const string RemoveDiscountCoupon = "remove_discount_coupon";
}
```

(`DeleteProduct` allowlist'lerde kullanılmaz; tamlık için tanımlanır.)

- [ ] **Step 2: Provider imzasını + filtrelemeyi güncelle**

`src/ChatAgent/McpToolProvider.cs` dosyasının TAMAMINI aşağıdakiyle değiştir:

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;

namespace ChatAgent;

// MCP tool'larini keşfeder (ListTools) ve verilen allowlist'e gore filtreler. Token okumaz;
// Authorization, MCP HttpClient'ina takili TokenInjectingHandler tarafindan her cagriya
// iliştirilir (user token, yoksa m2m).
public interface IMcpToolProvider
{
    Task<IList<McpClientTool>> GetToolsAsync(
        string serverName, string url, IReadOnlyCollection<string> allowedTools, CancellationToken ct = default);
}

public sealed class RequestScopedMcpToolProvider(
    HttpClient httpClient,
    ILogger<RequestScopedMcpToolProvider> logger) : IMcpToolProvider
{
    public async Task<IList<McpClientTool>> GetToolsAsync(
        string serverName, string url, IReadOnlyCollection<string> allowedTools, CancellationToken ct = default)
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

            var all = await client.ListToolsAsync(cancellationToken: ct);

            // Yalnizca allowlist'teki tool'lari birak; bilinmeyen/yeni tool asla eklenmez (fail-safe).
            var filtered = all.Where(t => allowedTools.Contains(t.Name)).ToList();

            // Allowlist'te olup sunucunun sunmadigi isimler = yazim hatasi/rename; sessiz kaybi onlemek icin uyar.
            var missing = allowedTools.Where(n => all.All(t => t.Name != n)).ToList();
            if (missing.Count > 0)
                logger.LogWarning("MCP '{Server}': allowlist'teki tool(lar) sunucuda bulunamadi: {Missing}",
                    serverName, string.Join(", ", missing));

            return filtered;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MCP '{Server}' tool kesfi basarisiz; bu istek icin atlandi.", serverName);
            return [];
        }
    }
}

public static class McpToolProviderExtensions
{
    // Verilen MCP server'larin allowlist'e gore filtrelenmis tool'larini tek listede toplar
    // (agent factory icinde). Her server girisi izin verilen tool adlarini ZORUNLU belirtir.
    public static IList<AITool> CollectTools(
        this IMcpToolProvider provider, params (string Name, string Url, string[] AllowedTools)[] servers)
    {
        List<AITool> tools = [];
        foreach (var (name, url, allowedTools) in servers)
            tools.AddRange(provider.GetToolsAsync(name, url, allowedTools).GetAwaiter().GetResult());
        return tools;
    }
}
```

Not: `.Where`/`.All`/`.ToList` için `System.Linq` gerekir; ChatAgent'ta ImplicitUsings açık olduğundan ayrı `using` gerekmez. Derleme "are you missing a using" hatası verirse dosyanın başına `using System.Linq;` ekle.

- [ ] **Step 3: public agent'a catalog read allowlist'i geçir**

`src/ChatAgent/Program.cs` — public agent factory'sindeki `CollectTools` çağrısını değiştir. Şu satır:

```csharp
    var tools = sp.GetRequiredService<IMcpToolProvider>()
        .CollectTools((McpServers.Catalog, catalogUrl));
```

şununla değişir:

```csharp
    var tools = sp.GetRequiredService<IMcpToolProvider>()
        .CollectTools((McpServers.Catalog, catalogUrl,
            [CatalogTools.SearchProducts, CatalogTools.GetProduct]));
```

- [ ] **Step 4: assistant agent'a catalog read + basket allowlist'i geçir**

`src/ChatAgent/Program.cs` — assistant agent factory'sindeki `CollectTools` çağrısını değiştir. Şu satır:

```csharp
    var tools = sp.GetRequiredService<IMcpToolProvider>()
        .CollectTools((McpServers.Catalog, catalogUrl), (McpServers.Basket, basketUrl));
```

şununla değişir:

```csharp
    var tools = sp.GetRequiredService<IMcpToolProvider>()
        .CollectTools(
            (McpServers.Catalog, catalogUrl, [CatalogTools.SearchProducts, CatalogTools.GetProduct]),
            (McpServers.Basket, basketUrl, [BasketTools.AddToCart, BasketTools.GetBasket,
                BasketTools.RemoveBasketItem, BasketTools.ApplyDiscountCoupon, BasketTools.RemoveDiscountCoupon]));
```

- [ ] **Step 5: ChatAgent derlensin**

Run: `dotnet build src/ChatAgent/ChatAgent.csproj`
Expected: Build succeeded, 0 error. (Imza değişikliğinin tüm çağrı yerlerini yakaladığını doğrular.)

- [ ] **Step 6: Tüm çözüm derlensin (regresyon yok)**

Run: `dotnet build ECommerceWithAgentFramework.slnx`
Expected: Build succeeded, 0 error.

- [ ] **Step 7: Commit**

```bash
git add src/ChatAgent/ConstValues.cs src/ChatAgent/McpToolProvider.cs src/ChatAgent/Program.cs
git commit -m "feat(chat-agent): allowlist MCP tools per agent"
```

---

## Self-Review Notu

- Spec kapsamı → görev: sabitler (Step 1), provider filtre+uyarı+imza (Step 2), public allowlist (Step 3), assistant allowlist (Step 4). Tümü karşılandı.
- Tip tutarlılığı: `GetToolsAsync(..., IReadOnlyCollection<string> allowedTools, ...)`; tuple `string[] AllowedTools` (string[] IReadOnlyCollection<string>'i uygular); sabit adları `CatalogTools`/`BasketTools` her yerde aynı.
- Fail-safe: filtre allowlist üyeliğiyle; `delete_product` hiçbir allowlist'te yok → hiçbir agent'a gelmez.
- Placeholder yok; her kod adımı tam içerik taşır.
- Doğrulama: build (test yok). İşlevsel kontrol spec'te not edildi (çalışma anı: toplanan tool'larda delete_product olmamalı).