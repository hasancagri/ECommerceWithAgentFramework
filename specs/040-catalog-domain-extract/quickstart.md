# Quickstart: Catalog Domain Extract — Canlı Doğrulama

Amaç: extract sonrası davranış eşitliğini kanıtlamak (SC-001..SC-005). Kontratlar: `contracts/frozen-contracts.md`.

## Ön koşullar

- Docker çalışır (Postgres + RabbitMQ Aspire kaldırır); OpenAI anahtarı config'te (IngestionAgent + ChatAgent).
- catalogDb + storefrontDb volume'ları SIFIRLANIR (K10): `docker volume rm` ile ilgili volume'lar silinir
  (Aspire kapalıyken) — katalog feed replay ile yeniden kurulacak.

## Adımlar

1. Derle + testler: `dotnet build` ve `dotnet test` — hepsi yeşil (SC-001, SC-005).
2. Sistemi kaldır: `dotnet run --project src/aspire/AppHost/AppHost.csproj`.
3. Feed ingest bekle: Hangfire pull → Supplier.Gateway → IngestionAgent zinciri koşar.
   Beklenen: loglarda BrandWrite/CategoryWrite/CatalogWrite/StockWrite başarılı; DLQ boş (SC-002).
4. Vitrin: WebApp ana sayfa ürünleri listeler; ürün detay fiyat/görsel/kategori gösterir (SC-002).
5. Arama: WebApp aramasından mevcut bir ürün adıyla sonuç döner (hybrid search çalışır) (SC-002).
6. Sepet + checkout: ürün sepete eklenir, checkout tamamlanır, sipariş Confirmed olur (SC-003).
7. Chat: chat sayfasından ürün sorulur (örn. "X ürünü var mı"); agent MCP tool'la yanıt döner (SC-004).

## Beklenen sonuçlar

- Ürün sayısı ve vitrin davranışı extract öncesiyle aynı; Gtin alanı DB'de null (kabul).
- `catalogDb.customnop` DEĞİL — mevcut Catalog şeması kullanılır; Marten belgeleri yeni şekle yazılmıştır.
- Storefront satırları decimal fiyat taşır; WebApp ekranlarında fark görünmez.

## Bilinen sınırlar

- ProductTag'ın dış yüzeyi yok; yalnız birim testle doğrulanır.
- Grouped ürün/Dimensions/Seo alanları boş; akışları bu feature'da doğrulanmaz (041+ konusu).
