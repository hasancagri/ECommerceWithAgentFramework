# Data Model: Storefront Semantic Search

**Feature**: 067 | **Date**: 2026-09-07

Yeni aggregate YOK. Bir yeni read-model yol-arkadaşı dokümanı + iki Options POCO'su. (REVİZE
2026-09-07 implement: "StorefrontView'a tek alan" kararı devrildi — 1536 float JSONB'de ~17KB;
view içinde olsaydı tüm tam-satır okuma yolları şişerdi. Kullanıcı onayıyla ayrı doküman.)

## ProductDescriptionEmbedding (YENİ doküman)

Dosya: `src/services/storefront/Storefront.Api/Domains/StorefrontView/ProductDescriptionEmbedding.cs`

| Alan | Tip | Anlam |
|---|---|---|
| `ProductId` | `Guid` (PK) | StorefrontView ile PK-eş. |
| `Vector` | `float[]` (1536) | Açıklamadan türetilen anlamsal temsil. Kullanıcıya asla gösterilmez; yalnız kNN sıralama/benzerlikte. |

- Satır YOKSA = temsil yok (açıklama boş YA DA henüz üretilmedi). Boş açıklama satırı SİLER.
- Yazan: YALNIZ `ProductChangedEvent` handler'ı (view ile AYNI transaction) + `EmbeddingBackfillService`.
- Yaşam-döngüsü durumu TAŞIMAZ: `IsDeleted` üretimi etkilemez; görünürlük her sorguda StorefrontView
  satılabilirlik filtresiyle sağlanır (`!IsDeleted && Name != null && Price != null`) — FR-007.
- Optimistic concurrency bilinçli YOK: handler/backfill yarışında son yazan kazanır (aynı metnin temsili).

### Yeniden-embedding karar tablosu (saf mantık — test-first, İLKE VI)

Karar yardımcısı: `StorefrontView.DecideEmbedding(new, old, hasEmbedding)` → `Keep/Generate/Clear`;
handler ve backfill aynı karara uyar.

| Eski açıklama | Yeni açıklama | Mevcut temsil | Sonuç |
|---|---|---|---|
| * | boş/null | * | Clear (temsil satırı silinir) |
| X | X (değişmedi) | var | Keep (API çağrısı yok) |
| X | X (değişmedi) | yok | Generate (backfill/ilk kurulum durumu) |
| X | Y (değişti) | * | Generate (yeniden üret) |

Fiyat/stok/puan güncellemeleri (açıklama sabitken) embedding tetiklemez.

## SemanticSearchOption (yeni Options POCO)

Dosya: `src/services/storefront/Storefront.Api/Options/SemanticSearchOption.cs` —
`AddOptions<T>().BindConfiguration(nameof(SemanticSearchOption)).ValidateDataAnnotations().ValidateOnStart()`.

| Alan | Default | Anlam |
|---|---|---|
| `MaxCosineDistance` | `0.68` | Kosinüs mesafe eşiği; üstü "alakasız" sayılır (FR-005/SC-005). Gerçek veriyle KALİBRE (2026-09-07): TR↔EN isabetler 0.56-0.64, alan-dışı ≥0.70; 0.55 TR sorguları eliyordu. |
| `BackfillBatchSize` | `500` | Backfill'de tek OpenAI isteğine giden metin sayısı (SC-004). |

## OpenAiOption (Storefront'a yeni; ChatAgent emsali)

Dosya: `src/services/storefront/Storefront.Api/Options/OpenAiOption.cs` — `BindConfiguration("OpenAI")`.

| Alan | Default | Anlam |
|---|---|---|
| `ApiKey` | — `[Required]` | Fail-fast: eksikse servis açılmaz (CLAUDE.md OpenAI listesine Storefront eklenir). |
| `EmbeddingModel` | `text-embedding-3-small` | 1536 boyut; boyut değişimi tüm embedding'lerin yeniden üretimini gerektirir (bilinçli sabit). |

## İlişkiler / değişmeyenler

- `ProductChangedEvent` kontratı DEĞİŞMEZ (Description zaten taşınıyor) — cross-BC etki yok.
- Facet/list sorguları mevcut satılabilirlik tanımını aynen kullanır; embedding yalnız semantik
  yolda ek `!= null` koşulu getirir (açıklamasız ürün yapısal aramada normal bulunur — edge case).
- Varyant ailesi (`FamilyCode`) semantik sonuçlarda MVP'de gruplanmaz; tool zaten tekil ürün satırı
  döner (mevcut `search_storefront_products` davranışıyla tutarlı).