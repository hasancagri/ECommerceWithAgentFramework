# Data Model: Tek Müşteri MCP Fasadı

Fasad **DB'siz** — kalıcı depo yok. Modeller config + süreç-içi (in-memory) yapılardır. Domain verisi
alt BC'lerde kalır (İlke I). Aşağıdakiler fasadın yönlendirme + toplama + kimlik yapılarıdır.

## Config: FacadeOption (downstream registry kaynağı)

Section `FacadeOption` (Options pattern). Fasadın hangi BC'leri, hangi yüzeyde topladığını + keşif makine
kimliğini tanımlar.

| Alan | Tip | Not |
|---|---|---|
| `Downstreams` | `DownstreamBc[]` | Toplanacak BC listesi |
| `DiscoveryClientId` | string | ListTools makine kimliği (`mcp-gateway-discovery`) |
| `DiscoveryClientSecret` | string | client_credentials secret |
| `CacheTtlSeconds` | int | Tool-katalog kısa cache (ör. 60); 0 = cache yok |

### DownstreamBc

| Alan | Tip | Not |
|---|---|---|
| `Name` | string | BC adı (log/teşhis) |
| `McpUrl` | string | BC `/mcp` adresi (service discovery, ör. `http://basket-api/mcp`) |
| `Surface` | enum `customer`\|`admin` | Hangi fasad ucunda görünür |
| `RequiresUserAuth` | bool | Tool'ları korumalı mı (checkout/hesap) yoksa anonim mi (katalog/vitrin) |

## In-memory: ToolRoutingRegistry (saf; test-first)

Toplanan tool'lardan türetilen ad→sahip eşlemesi. `ListTools` sonrası kurulur.

| Alan | Tip | Not |
|---|---|---|
| `ToolName` | string | Global benzersiz tool adı (`Shared/McpToolNames`) |
| `OwnerBc` | `DownstreamBc` | Çağrının yönleneceği BC |
| `Surface` | enum | müşteri/admin |

- Değişmez kural: bir ad tek sahibe eşlenir. Aynı ad iki BC'den gelirse deterministik ilk-sahip seçilir + loglanır.
- `Resolve(toolName) → OwnerBc?` (bulunamazsa çağrı açıklayıcı hata ile reddedilir).

## In-memory: AggregatedToolCatalog

Oturum anında toplanan, yüzeye göre süzülmüş birleşik tool listesi (kısa-TTL cache'lenebilir).

| Alan | Tip | Not |
|---|---|---|
| `Surface` | enum | müşteri/admin |
| `Tools` | `McpToolSchema[]` | Downstream ListTools'tan toplanan şemalar (yüzey süzülü) |
| `CollectedAt` | DateTimeOffset | TTL değerlendirmesi |

- Erişilemez BC → o BC'nin tool'ları katalogda yok (graceful degrade); sonraki toplama denemesinde döner.

## Kimlik: AnonymousCartIdentity (süreç-içi/başlıkta)

Giriş öncesi sepet sahipliği. Fasad oturuma/cihaza bağlı opak bir anahtar üretir/taşır (X-User-Key yan
yolu; 061/057). Kalıcı kullanıcı kaydı DEĞİL.

| Alan | Tip | Not |
|---|---|---|
| `UserKey` | string (opak) | Anonim sepet sahibi; Basket'e X-User-Key ile taşınır |
| (login sonrası) | — | Checkout step-up login anında Basket `MergeFrom(UserKey → userId)` ile devredilir (057) |

## Yüzey ayrımı (SurfaceFilter — saf; test-first)

`/mcp` (müşteri) → `Surface==customer` tool'ları; `/mcp-admin` (admin) → `Surface==admin`. Çapraz sızıntı
yasak (070 `ConfigureSessionOptions` yol-prefix budaması emsali).

## İlişkiler

```
FacadeOption.Downstreams ──(lazy ListTools, makine token)──► AggregatedToolCatalog ──► ToolRoutingRegistry
Claude Desktop ──CallTool(ad)──► [Resolve ad→BC] ──(kullanıcı bearer / anonim X-User-Key)──► sahip BC /mcp
checkout step-up login ──► Basket.MergeFrom(anonUserKey → userId)  (sepet devri, 057)
```

## Not

- Kalıcı depo yok → migration/şema yok. Registry + katalog süreç ömrü (kısa cache) içinde.
- Kart/PAN modeli YOK: yeni kart PG hosted form'da; fasad yalnız tokenize kart seçimini proxy'ler.