# Contracts: Yazıcı Agent Sözleşmeleri (015)

**Date**: 2026-07-27 | **Plan**: [../plan.md](../plan.md)

Yeni dış kontrat YOK; bu dosya iç sözleşmeleri ve "değişmez" taahhütlerini sabitler.

## Değişmeyen dış kontratlar

- RabbitMQ: exchange `supplier.product-snapshot`, kuyruk `ingestion.supplier-product-snapshot`, DLQ `ingestion.supplier-product-snapshot.dlq`.
- Event kontratı: `SupplierProductSnapshotReceived` (`Shared.IntegrationEvents`) değişmez.
- MCP tool imzaları değişmez: `upsert_product`, `set_stock`, `set_product_discount`, `remove_product_discount`.
- Retry politikası: `IngestionWriteException` → 10s/30s/60s cooldown → DLQ (Program.cs mevcut blok).

## Agent → tool allowlist (FR-009)

| Agent | MCP sunucusu | İzinli tool'lar |
|-------|--------------|-----------------|
| catalog-writer | catalog-api `/mcp` | `upsert_product` |
| stock-writer | stock-api `/mcp` | `set_stock` |
| discount-writer | discount-api `/mcp` | `set_product_discount`, `remove_product_discount` |

Allowlist dışı tool agent'a hiç verilmez (keşifte filtrelenir); bilinmeyen tool eklenemez.

## WriterResult çıktı şeması (structured output)

```json
{ "isSuccess": true, "error": null, "productId": "guid — yalnız catalog varyantı" }
```

- `isSuccess=false` → `error` zorunlu (`KOD` veya `KOD: detay`).
- catalog: `isSuccess=true` → `productId` zorunlu; stok/indirim varyantında alan yoktur.
- Deserialize edilemeyen çıktı = adım başarısızlığı (sessiz başarı yasak — SC-002).

## Prompt sözleşmesi (üç yazıcının ortak kuralları)

- Girdinin tamamı kod tarafından mesaja gömülür (SKU, ad, fiyat, adet, oran, ProductId); LLM'e keşif bırakılmaz.
- Tool çağrısı zorunludur; tool çağrılmadan `isSuccess=true` dönmek yasaktır.
- Tool zarfı `isSuccess=false` ise sonuç `isSuccess=false` + zarftaki hata kodu aktarılır.
- Discount kuralı: `DiscountPercent` boş → `remove_product_discount`; dolu → `set_product_discount` (ikisi de idempotent).
- Temperature 0; adım başına sınırlı tool-iterasyonu.

## Config sözleşmesi

| Anahtar | Zorunlu | Not |
|---------|---------|-----|
| `OpenAI:ApiKey` | evet | yoksa açılışta throw (FR-014) |
| `OpenAI:Model` | evet | default YOK — bilinçli (R7) |
| `Ingestion:StepTimeoutSeconds` | hayır | default 60; adım başına LLM+tool bütçesi (R5) |