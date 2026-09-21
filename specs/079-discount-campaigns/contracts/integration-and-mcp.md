# Contracts: Integration Events · MCP Admin Tools · Scopes

## Integration Events

### Yayınlanan: `ProductDiscountChanged` (Discount.Api → Storefront)

`src/others/Shared/IntegrationEvents.cs`'e eklenir. Bir kitabın tek indiriminin penceresini iter; kitabın
indirimi kaldırılınca (bitiş/iptal) temizlik için sıfır/null gönderilir.

```csharp
public record ProductDiscountChanged(
    Guid ProductId,
    int DiscountPct,        // 0 = indirim yok (temizle); 1-99 = kitabın aktif/gelecek indirimi
    DateTime? StartsAt,     // pencere başlangıcı (null = temizlik)
    DateTime? EndsAt);      // pencere bitişi (null = süresiz ya da temizlik)
```

- **Yayıncı**: Discount.Api exchange'i (fanout) deklare eder; `CreateCampaign` (aktifse) +
  `CampaignActivated`/`CampaignEnded` fire'ında yayınlar (kitap başına).
- **Tüketici**: Storefront `DiscountConsumers` — binding'i TÜKETİCİ kurar (007 dersi); `StorefrontView.ApplyDiscount`.
- **RabbitMqConstants**: yeni `discount.product-discount-changed` exchange + `storefront.events` kuyruğuna binding.

### Tüketilen: `ProductChangedEvent` (Catalog → Discount.Api)

Mevcut event (`IntegrationEvents.cs:20-37`). Discount.Api `CatalogConsumers` ile `ProductCatalogRef`
{productId, categoryId, authorIds, publisherId, published} upsert eder (süzgeç çözümü; fiyat/isim alınmaz).

## MCP Admin Tools (`/mcp-admin`, scope `AdminDiscountWrite`)

`Shared/McpToolNames.cs`'e yeni `DiscountAdminTools` sınıfı; Program.cs `ConfigureSessionOptions`
`discountAdminToolNames` allowlist (070/074 deseni — yeni tool eklerken allowlist'e EKLE).

| Tool | Slice | Girdi | İş |
|---|---|---|---|
| `admin_create_campaign` | `Features/Agents/Commands/CreateCampaign` | name, scopeType (category/author/publisher/product), scopeRef, percentage, startsAt, endsAt? | Campaign.Create → süzgeci kitap setine çöz → her kitaba `ProductDiscount` **yoksa** ekle (varsa atla) → aktifse `ProductDiscountChanged` it → `ScheduleAsync(start,end)`. Yanıt: {applied, skipped} özet |
| `admin_cancel_campaign` | `.../CancelCampaign` | campaignId | Campaign.Cancel → o campaign'in `ProductDiscount`'larını sil → `ProductDiscountChanged(pct:0)` it |
| `admin_list_campaigns` | `Features/Agents/Queries/ListCampaigns` | status? filter | Kampanya listesi (admin görünürlük) |

**Edit tool YOK** (v1) — süzgeç snapshot + kitap-başı-tek-indirim'de düzenleme belirsiz; iptal-edip-yeniden-aç yeter.

MCP tool sarmalayıcısı slice'la AYNI dosyada (`[McpServerToolType]`, dosya sonu); `ICurrentUser.Load(
http.HttpContext.User)` ile admin kimliği; `IMessageBus.InvokeAsync` ile handler.

## Yeni Scopes (İLKE V)

`KnownScopes` registry + rol→scope map DB seed'ine eklenir:

| Scope | Kullanım | Kim taşır |
|---|---|---|
| `discount.read` | Checkout gRPC `GetProductDiscounts` | Order.Api makine token (SagaTokenHandler) |
| `AdminDiscountWrite` (`discount.admin.write`) | `/mcp-admin` kampanya tool'ları | admin rolü (`external-admin-agent`) |

- `SagaTokenHandler` scope demetine `discount.read` eklenir (Order→Discount S2S).
- Discount.Api `/mcp` anonim keşif YOK — yalnız korumalı `/mcp-admin` (kampanya = admin işi; müşteri
  yüzeyi indirimi vitrinde `query_storefront`'tan görür, Discount.Api'ye doğrudan bağlanmaz).