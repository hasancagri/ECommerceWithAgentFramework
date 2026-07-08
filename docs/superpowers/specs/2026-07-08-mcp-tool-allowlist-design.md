# ChatAgent MCP Tool Allowlist — Tasarım

**Tarih:** 2026-07-08
**Durum:** Onaylandı

## Amaç

ChatAgent'taki agent'ların bir MCP server'ın **tüm** tool'larını değil, yalnızca kendilerine
açıkça izin verilen tool'ları toplamasını sağlamak. Böylece bir tool'un LLM'e sunulmaması,
prompt içinde "şuna asla dokunma" talimatıyla değil, tool'u agent'ın araç setine **hiç
eklememekle** garanti edilir.

Somut problem: catalog MCP server'ı `get_product`, `search_products`, `delete_product`
tool'larını sunar. Bugün `CollectTools((Catalog, url))` bunların **hepsini** toplar; anonim
(public) agent bile `delete_product`'ı alır. Onu durduran tek şey prompt cümlesi ve
handler'daki `[RequiredScope]` reddidir.

## Kapsam Kararları (kullanıcı onaylı)

- Filtre **ChatAgent'ta**, tool-adı bazlı bir **allowlist** olarak uygulanır (servis tarafı
  audience etiketi veya ayrı MCP endpoint'leri DEĞİL).
- Allowlist **her iki agent'a** da uygulanır (public + assistant).
- Allowlist **zorunludur**: her server girişi izin verilen tool adlarını belirtmek
  zorundadır. Implicit "hepsini al" seçeneği yoktur → catalog'a ileride eklenecek bir yazma
  tool'u varsayılan olarak DIŞARIDA kalır (fail-safe).

## Güvenlik Notu (kapsam sınırı)

Bu özellik bir **tool-sunum (exposure)** katmanıdır, yetki sınırı değildir. Gerçek yetki
kontrolü hâlâ her servisin handler'ındaki `[RequiredScope]` middleware'idir (defense-in-depth)
ve bu tasarımda **kaldırılmaz**. Allowlist, LLM'in yanlış tool'u çağırmaya kalkışmasını en
baştan engeller; `[RequiredScope]` ise gerçek yetkiyi zorlar. İkisi bir arada durur.

## Değişiklikler (3 dosya)

### 1. `src/ChatAgent/ConstValues.cs`

Mevcut `McpServers` kalıbını izleyen tool-adı sabitleri eklenir (magic string tekrarını
önlemek için). Değerler, servislerdeki `[McpServerTool(Name = ...)]` ile birebir aynıdır:

```csharp
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

(`DeleteProduct` sabiti allowlist'lerde KULLANILMAZ; tamlık ve okunabilirlik için tanımlanır.)

### 2. `src/ChatAgent/McpToolProvider.cs`

Filtreleme ve eksik-isim uyarısı, `ILogger`'a sahip olan provider içinde yapılır (statik
extension'ın logger'a erişimi yok):

- `IMcpToolProvider.GetToolsAsync` imzasına `IReadOnlyCollection<string> allowedTools` parametresi
  eklenir. `ListToolsAsync` sonucu `allowedTools.Contains(tool.Name)` ile filtrelenir; yalnızca
  izin verilen tool'lar döner.
- Allowlist'te olup server'ın `ListTools` cevabında bulunmayan her isim için `LogWarning`
  (yazım hatası / rename tespiti). Bilinmeyen bir tool asla eklenmez.
- `CollectTools` extension imzası şu hale gelir:
  `params (string Name, string Url, string[] AllowedTools)[] servers` — her server için
  `GetToolsAsync(name, url, allowedTools)` çağrılır. Allowlist boş dizi verilirse o server'dan
  hiçbir tool toplanmaz (zorunlu ve açık davranış).
- Mevcut hata davranışı korunur: bir server'ın tool keşfi (bağlantı vb.) başarısız olursa o
  istek için boş liste döner ve loglanır (agent yine ayağa kalkar).

### 3. `src/ChatAgent/Program.cs`

İki agent factory'si allowlist geçirir:

- **public** (anonim):
  ```csharp
  .CollectTools((McpServers.Catalog, catalogUrl,
      [CatalogTools.SearchProducts, CatalogTools.GetProduct]))
  ```
  → `delete_product` artık toplanmaz.

- **assistant** (login):
  ```csharp
  .CollectTools(
      (McpServers.Catalog, catalogUrl, [CatalogTools.SearchProducts, CatalogTools.GetProduct]),
      (McpServers.Basket, basketUrl, [BasketTools.AddToCart, BasketTools.GetBasket,
          BasketTools.RemoveBasketItem, BasketTools.ApplyDiscountCoupon,
          BasketTools.RemoveDiscountCoupon]))
  ```
  → tüm basket tool'ları gelir, ama catalog'dan `delete_product` DIŞARIDA kalır.

## Doğrulama

- Test projesi yok → `dotnet build ECommerceWithAgentFramework.slnx` temiz derlenmeli.
- İşlevsel kontrol (çalışma anı): public ve assistant agent'ların topladığı tool listesinde
  `delete_product` bulunmamalı; assistant'ta basket tool'ları bulunmalı. Gerekirse factory
  içinde toplanan tool adlarını bir kez loglayarak doğrulanabilir (kalıcı kod değil).

## Dokunulmayanlar

- Servislerdeki MCP tool tanımları / `[RequiredScope]` middleware'i.
- Gateway MCP route'ları.
- Agent'ların Singleton olması ve per-user token'ın tool çağrılarına akmaması (ayrı,
  ertelenmiş borç — [[orchestrator-agent-singleton]]). Bu tasarım onunla ilgisizdir.