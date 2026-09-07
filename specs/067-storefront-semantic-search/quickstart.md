# Quickstart: Storefront Semantic Search (canlı doğrulama)

**Feature**: 067 | Kontratlar: [contracts/mcp-tools.md](contracts/mcp-tools.md) |
Model: [data-model.md](data-model.md)

## Ön koşullar

1. OpenAI anahtarı — Storefront artık fail-fast:
   ```bash
   dotnet user-secrets set OpenAI:ApiKey <key> --project src/services/storefront/Storefront.Api/Storefront.Api.csproj
   ```
2. Sistem her zaman Aspire'dan:
   ```bash
   dotnet run --project src/aspire/AppHost/AppHost.csproj
   ```
3. **Semantik senaryolar (S2, S3, S5) açıklama verisi ister** — bugün katalog %0 description
   (OpenLibrary fetch ayrı işte). Description'lı books.json + reseed sonrası koşulur. S1/S4 (yapısal)
   veriden bağımsız hemen koşar. Bu feature'ın bitmişliği description doldurmaya REHİN DEĞİL (spec
   Assumption).

## S1 — Keşif listeleri (US3, SC-003) — hemen koşar

Kök chat'e sırayla: "hangi kategoriler var", "hangi yazarlardan kitap var", "hangi yayınevleri var".

- Beklenen: gerçek listeler döner (bugünkü "yanıtlayamam" durumundan çıkış). Yazar listesi
  ProductCount çoktan aza + "toplam N yazar" bilgisi.
- Negatif kontrol: admin'den bir ürünü yayından kaldır (058 ekranı) → tek-ürünlü kategorisi
  listeden düşer (cache TTL 60sn bekle ya da ProductChangedEvent invalidasyonunu gözle).

## S2 — Backfill (FR-008, SC-004)

Description'lı reseed sonrası AppHost'u başlat; Storefront loglarında backfill'i izle.

- Beklenen: "backfill" log satırları; 20k kayıt dakikalar içinde. Bitişte kontrol:
  ```sql
  -- storefrontDb
  select count(*) filter (where v.data->>'Description' <> '') as kalan
  from storefrontmanagement.mt_doc_storefrontview v
  where not exists (select 1 from storefrontmanagement.mt_doc_productdescriptionembedding e where e.id = v.id);
  ```
  `kalan = 0` (yayından kaldırılmışlar dahil — embedding IsDeleted'dan bağımsız).
- İkinci restart: backfill no-op (log'da "0 kalan" / hızlı bitiş).

## S3 — Hibrit temalı arama (US1, SC-001)

Chat'e tek cümle: "kışın okunacak sürükleyici bilim kurgu, 300 TL altı, X yayınevi hariç".

- Beklenen: dönen HER sonuç fiyat<300 ve yayınevi≠X (SC-001); sıralama anlamca yakınlık.
- Ayrıştırma kontrolü: fiyat/yayınevi yapısal parametrelere, tema `semanticQuery`'ye gitmiş olmalı
  (ChatAgent log / tool-call argümanları).
- Dışlama tek başına: "Tolkien hariç fantastik öner" → sonuçlarda Tolkien yok (FR-003).
- Negatif (FR-007): sonuçtaki bir kitabı admin'den yayından kaldır → aynı sorgu tekrarında o kitap
  dönmez (satılabilirlik filtresi semantik yolda da çalışır).

## S4 — Genişleyen yapısal parametreler — hemen koşar

Chat: "Yapı Kredi Yayınları'ndan roman var mı 200 TL altı" → publisher + category + maxPrice
parametreleriyle tool çağrısı, sonuçlar kısıtlara uyar. (semanticQuery boş — mevcut Name ASC yolu.)

## S5 — "Bulunamadı" dürüstlüğü (SC-005) + benzer kitap (US2, SC-002)

1. Alakasız sorgu: "traktör motoru rektifiye el kitabı" (katalogda yok) → "bulunamadı" cevabı;
   zorla "benzer buldum" listesi YOK.
2. Chat'te bir kitaptan bahset → "buna benzer ne var" → kendisi hariç, satılabilir en yakın N kitap.
3. Embedding'siz ürün için benzer iste → kibar "benzer bulunamadı" (hata/exception yok, SC-002).
4. Eşik kalibrasyonu: 1. adım yanlış-pozitif veriyorsa `SemanticSearchOption:MaxCosineDistance`
   düşür (örn. 0.45), gerçek benzerler kaçıyorsa yükselt; appsettings üzerinden tek satır.

## S6 — Birim testleri + guard'lar

```bash
dotnet test tests/Storefront.Api.Tests/Storefront.Api.Tests.csproj   # NeedsReembedding test-first birimleri
dotnet build                                                          # tüm çözüm
scripts/check-flow-links.sh                                           # FLOW.md anchor guard (İLKE VII)
scripts/check-claude-spec-links.sh                                    # BC haritası spec yolları
```

- Beklenen: hepsi yeşil; FLOW.md'de yeni anlamsal-temsil adımı anchor'larıyla mevcut.