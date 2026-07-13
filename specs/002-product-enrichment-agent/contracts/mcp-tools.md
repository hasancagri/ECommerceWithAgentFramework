# Phase 1 Contracts: MCP Tools

Bu feature'ın dış yüzeyi MCP tool sözleşmeleridir (REST değil). Tümü ince sarmalayıcı:
aynı Wolverine command/query'sini `IMessageBus` ile çağırır (anayasa III).

## Catalog.Api — yeni tool'lar

### `list_incomplete_products` (okuma, scope: catalog.read)

Eksik (satışa-hazır olmayan) ürün adaylarını döner (FR-001).

- **Girdi**: yok (ops. `limit`).
- **Slice**: `Features/Agent/ListIncompleteProducts` → query `IsComplete == false`,
  `IsDeleted == false`.
- **Çıktı** (`FeatureListResultModel<Item>`): her item →
  `{ Id, Name, Brand, HasDescription, HasImage }`.

### `set_product_description` (yazma, scope: catalog.write)

Ürünün açıklamasını **yalnızca boşsa** yazar (FR-005, idempotent).

- **Girdi**: `{ Id: Guid, Description: string }`
- **Slice**: `Features/Commands/SetProductDescription` `[Transactional]` →
  `product.SetDescriptionIfEmpty(desc)`.
- **Çıktı** (`FeatureObjectResultModel<{ Id, Outcome }>`): Outcome = Written | Skipped.
  Ürün yok/deleted → NotFound.

### `set_product_image` (yazma, scope: catalog.write)

Ürünün görsel URL'ini **yalnızca boşsa** yazar (FR-005).

- **Girdi**: `{ Id: Guid, ImageUrl: string }`
- **Slice**: `Features/Commands/SetProductImage` `[Transactional]` →
  `product.SetImageUrlIfEmpty(url)`.
- **Çıktı**: `{ Id, Outcome: Written | Skipped }`. Yazınca RecalculateCompleteness
  ürün tamsa IsComplete=true → (IsActive ise) satışa çıkar.

## File.Api — yeni tool

### `upload_product_image` (yazma, scope: file.write)

Görsel byte'larını **ProductId'ye göre idempotent** alır ve deterministik URL döner (DB yok).

- **Girdi**: `{ ProductId: Guid, ContentBase64: string, ContentType: string }`
- **Slice**: `Domains/Images/Features/Commands/UploadImage` (`IDocumentSession` yok) →
  `Images/{ProductId}.png` varsa atla, yoksa byte'ları yaz.
- **Çıktı** (`FeatureObjectResultModel<{ Url }>`): statik serve edilen public URL
  (ör. gateway üzerinden `/file/images/{ProductId}.png`).

> File.Api'ye eklenir: `app.MapMcp("/mcp")`, `UseStaticFiles` (Images klasörü),
> `file.write` scope. `Identity.Server` `file.api` resource'una scope eklenir.

## Auth / client

- Yeni client `enrichment.agent` (ClientCredentials): scope'lar `catalog.read`,
  `catalog.write`, `file.write`.
- Worker açılışta token alır; MCP çağrılarında `Authorization: Bearer` ekler
  (`ClientCredentialsTokenHandler`). Scope zorlaması downstream `[RequiredScope]` +
  `ScopeAuthorizationMiddleware` ile.

## Agent-içi tool (MCP değil) — OpenAI image

- `generate_product_image(prompt) : bytes` — Image agent/workflow adımı OpenAI
  `gpt-image-1` çağırır. Dış sözleşme değil; agent'ın iç yeteneği.