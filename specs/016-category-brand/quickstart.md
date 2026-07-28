# Quickstart: Kategori ve Marka (016) — doğrulama rehberi

Önkoşul: Docker (Postgres/RabbitMQ için), `OpenAI:ApiKey` (IngestionAgent). Ayrıntı kontratlar: [contracts/](contracts/).

## 1. Derle + birim testleri

```bash
dotnet build
dotnet test
```

Beklenen: Category/Brand fabrika+normalizasyon, Product BrandId/CategoryId, StorefrontView Category testleri yeşil.

## 2. Sistemi kaldır

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

Feed pull (Hangfire PullCron) tetiklenince gateway 500 kaydın hepsini yayınlar (category alanı + 300 yeni kayıt).
IngestionAgent kuyruğunun boşalmasını bekle (RabbitMQ management: `ingestion.supplier-product-snapshot`, DLQ boş).

## 3. Catalog doğrulaması

- `GET {catalog}/api/v1/products/brands` → feed'deki markalar kimlik+ad; tekrar yok (normalize teklik).
- `GET {catalog}/api/v1/products/categories` → feed kategorileri; kategorisiz kayıtlar liste dışı.
- pgAdmin/`catalogDb`: `mt_doc_product` satırlarında `BrandId` dolu (migrasyon + backfill → SC-004 %100).

## 4. Storefront doğrulaması

- `GET {storefront}/api/v1/storefront/products/filters` → categories/brands kimlik+ad; boş kategori yok (US1-3).
- `GET .../products?categoryId={id}` ve `...?category={ad}` → yalnız eşleşenler; toplam/sayfa sayısı filtreli (SC-005).
- `categoryId` + `brandId` birlikte → AND davranışı (US2-2).

## 5. UI doğrulaması (WebApp)

- `/Products`: kategori/marka filtre seçenekleri görünür; seçim + sayfalama birlikte çalışır (US1-2, SC-001).
- Ürün detayı: kategori ve marka görünür (US4-1). Ürün oluşturma formu: marka listesi Catalog'dan (enum yok).

## 6. Feed senaryoları (US3 + SC-002/003)

- `products.json`'da bir kayda YENİ marka/kategori yaz → pull sonrası filtrelerde görünür (elle adım yok).
- Bir kaydın kategorisini değiştir → üründe güncellenir ve liste/facet'e yansır.

## 7. Asistan (US4-2)

- Chat'e "X kategorisindeki ürünleri göster" → `search_products(category=...)` ile daraltılmış sonuç döner.