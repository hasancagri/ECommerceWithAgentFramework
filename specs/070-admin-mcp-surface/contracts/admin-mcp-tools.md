# Contract: Admin MCP Tool'ları (070)

Uçlar: her BC'de YENİ korumalı `/mcp-admin` (RequireAuthorization + RFC 9728 metadata; anonim `/mcp`
dokunulmaz). Gateway: `/mcp-admin/{catalog|stock|customer}` + PRM rotaları. Tool adları
`Shared/McpToolNames.cs`'te tek kaynak. Tüm description'lar "arayüz metni" kalitesinde (FR-015):
alan açıklaması + geçerli değerler + 1 kullanım örneği.

## Catalog `/mcp-admin` — scope: `catalog.write` (CatalogAdminTools)

| Tool | Parametreler | Dönüş | Not |
|---|---|---|---|
| `admin_list_products` | page (int, default 1), pageSize (int, default 20, max 50), q (string?, ad/ISBN) | items[{productId, name, isbn, price, isPublished, stock?}], totalCount, page | Yayında olmayan DAHİL. Stabil productId. AdminListProducts ikizi |
| `admin_get_product` | productId (Guid) | künye tamamı + yazarlar/yayınevi/kategori (ad+id) + isPublished + fiyat + specs | AdminGetProduct ikizi; tek çağrıda tam detay (US1-AS2) |
| `admin_update_product` | productId + değişen künye alanları (name?, description?, price?, authorIds?, publisherId?, categoryId? — hepsi opsiyonel, MCP default kuralı: null değil DEFAULT ver) | GÜNCEL ürün (admin_get_product şekli) | Tek ürün. Fiyat değişimi ProductPriceChange + ProductChangedEvent mevcut akışı tetikler. İz: AdminActionLog |
| `admin_set_published` | productId (Guid), published (bool) | güncel {productId, isPublished} | SetProductPublished ikizi; vitrinden düşme/geri gelme mevcut event akışıyla. İz |
| `admin_get_price_history` | productId (Guid) | [{oldPrice, newPrice, changedAt}] | GetProductPriceHistory ikizi |

## Stock `/mcp-admin` — scope: `stock.write` (StockAdminTools)

| Tool | Parametreler | Dönüş | Not |
|---|---|---|---|
| `admin_set_stock` | productId (Guid), quantity (int, mutlak, >=0) | güncel {productId, onHand} | SetStockQuantity ikizi. İz |
| `admin_adjust_stock` | productId (Guid), delta (int, +/-) | güncel {productId, onHand} | Artır/azalt (058); negatife düşüş reddi domain'de. İz |

## Customer `/mcp-admin` — scope: `merchant.credentials.write` (CustomerAdminTools)

| Tool | Parametreler | Dönüş | Not |
|---|---|---|---|
| `admin_get_merchant_status` | — | {configured (bool), merchantId?, updatedAt?} | MerchantKey ASLA dönmez (maskeli durum; mevcut GetMerchantInformation davranışı) |
| `admin_set_merchant_credentials` | merchantId (Guid), merchantKey (string) | {configured: true, merchantId} | Upsert; key yanıtta/izde düz metin YOK. İz (Summary: "credentials rotated") |
| `admin_submit_onboarding` | başvuru alanları (type, name, email, gsm, address, iban, contact*, koşullu vergi alanları) | PG yanıtı (durum + mesaj) | PG Merchant.Api MCP'sine S2S sarmalayıcı (makine kimliği içeride; anayasa sapması — bkz plan Complexity). İz |
| `admin_onboarding_status` | email (string) | {status, message, merchantId?, merchantKey?} | Approved'da Id+Key döner → admin `admin_set_merchant_credentials` ile kaydeder (ekransız kurtarma yolu) |

## Hata sözleşmesi

- Yetki: endpoint 401/403 (RFC 9728 challenge → OAuth akışı); handler `[RequiredScope]` ikinci katman.
- İş hatası: mevcut Result deseni — `messages[{code, property}]`; serbest metin yok.
- Bulunamadı: NotFound result (kod: ilgili ResourceConstants).
- Bulk YOK: hiçbir tool dizi/toplu parametre almaz (FR-010).
