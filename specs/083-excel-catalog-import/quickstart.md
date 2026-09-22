# Quickstart / Doğrulama: Excel Katalog Import

Uçtan-uca canlı doğrulama. Detay: [contracts](./contracts/mcp-tools.md), [data-model](./data-model.md).

## Önkoşullar

- Sistem Aspire AppHost'tan ayakta (`dotnet run --project src/aspire/AppHost/AppHost.csproj`).
- R2 credential + `RegistryBackfill` config'li (082); r2.dev public erişim AÇIK.
- Test verisi: `~/dev/catalog-data/catalog-import.xlsx` (19.711 satır, 14 kolon).
- Admin agent `/mcp-admin`'e bağlı (`external-admin-agent`, PKCE loopback).

## US1 — Import (P1)

1. Agent'tan: `admin_import_catalog` çağır → `{url, expiresAt}` döner.
2. Tarayıcıda url aç → yükleme formu. xlsx seç, gönder → "19.711 satır alındı".
3. Doğrula (Postgres catalogDb): `SELECT count(*) FROM mt_doc_importrow WHERE status='Processed'` zamanla → 19.711.
4. Ürün sayısı: benzersiz ISBN = ürün (TASLAK). `SELECT count(*) FROM ... WHERE published=false`.
5. **Idempotency:** aynı dosyayı tekrar yükle → yeni ürün OLUŞMAZ (Processed satır ISBN var → atla).
6. **Çökme-güvenli:** processor sürerken servisi kapat/aç → kaldığı yerden, çift ürün yok.

## US2 — Async kapak (P2)

1. Import'tan sonra kapağı R2'de olan bir ISBN seç (082 backfill'li, ör. 9781442499713 civarı).
2. Kısa süre içinde o ürünün `ImageUrl` r2.dev public URL'iyle dolduğunu doğrula (import bitmeden düşer).
3. Storefront read-model'de kapak yansımış (ProductChangedEvent).
4. **Kapaksız:** R2'de + local staging'de olmayan ISBN → ImageUrl boş kalır, hata yok (placeholder).

## US3 — Toplu yayın (P2)

1. Agent'tan `admin_publish_imported` çağır → `{publishedCount, skippedNoPriceCount}`.
2. Doğrula: fiyat>0 import ürünleri `published=true` + Storefront'ta görünür.
3. Fiyatsız import ürünleri taslak kalır (skipped sayısına dahil).
4. Elle oluşturulmuş (import-dışı) taslak varsa dokunulmamış.

## Edge

- Bozuk xlsx / kolon şeması uymaz yükle → hata sayfası, 0 satır. `get_import_status` failures listesi.
- Süresi geçmiş link (expiresAt sonrası GET) → nötr 404.
- Zorunlu alanı (ISBN) boş satır → o satır `Failed` + Error; kalan satırlar işlenir.

## Söküm doğrulaması (FR-011)

- `books.json` + `BookImportHostedService` + `Process/ImportBook.cs` yok; boot'ta seed import çalışmaz.
- Katalog yalnız Excel import ile dolar.

## Otomatik test

- `dotnet test tests/Catalog.Api.Tests/...` — ImportRow durum geçişi + publish_imported kapı mantığı (domain-TDD).