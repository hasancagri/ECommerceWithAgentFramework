# Implementation Plan: Storefront Semantic Search

**Branch**: `067-storefront-semantic-search` | **Date**: 2026-09-07 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/067-storefront-semantic-search/spec.md`

**Kademe**: Tam — yeni saklanan alan (embedding), dış AI bağımlılığı (Storefront'a OpenAI), MCP tool
kontrat değişimi + yeni tool'lar, kayda değer belirsizlik (benzerlik eşiği) vardı. Tam akış zorunlu.

## Summary

Storefront read-model'ine açıklama-embedding tabanlı semantik arama eklenir: `ProductDescriptionEmbedding`
yol-arkadaşı dokümanı (PK=ProductId, float[] 1536, OpenAI `text-embedding-3-small`; REVİZE — implement
bulgusuyla tek-alan yerine ayrı doküman, research R1); üretim
`ProductChangedEvent` handler'ında senkron (yalnız açıklama değişince), geçmiş katalog için startup
backfill. MCP yüzeyi genişler: `search_storefront_products`'a `semanticQuery` + kategori/yayınevi +
dışlama parametreleri; yeni `find_similar_books`, `list_categories`, `list_authors`,
`list_publishers`. Hibrit sorgu: yapısal filtre ÖNCE (Marten LINQ, mevcut semantikle) → kalan kümede
pgvector kosinüs kNN (parametreli ham SQL) + mesafe eşiği ("bulunamadı" dürüstlüğü). ChatAgent
allowlist + prompt güncellenir. Kaynak kararlar: brainstorm 2026-09-04/07 (memory
`text-first-discovery-design-decisions`), ayrıntı [research.md](research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Marten 9.5.0 + **Marten.PgVector 9.5.0 + Pgvector 0.3.2** (props'ta pinli,
ilk kez kullanılacak) · Wolverine (in-proc bus + RabbitMQ) · **Microsoft.Extensions.AI.OpenAI 10.7.0**
(`IEmbeddingGenerator`, ilk kez embedding için) · MCP server (mevcut `/mcp` yüzeyi) · Scrutor

**Storage**: storefrontDb (Postgres; Marten JSONB document store + `vector` extension —
`UsePgVector()` extension'ı şemaya ekler, `ApplyAllDatabaseChangesOnStartup` kurar)

**Testing**: xUnit + Shouldly (saf domain birimi: yeniden-embedding kararı test-first, İLKE VI)

**Target Platform**: Aspire AppHost altında koşan mikroservis; Storefront.Api + ChatAgent dokunulur

**Project Type**: Mevcut BC'ye (Storefront) VSA slice ekleme + agent wiring; yeni servis YOK

**Performance Goals**: kNN ~20k satırda exact scan ile <100ms; backfill 20k kaydı batch'li
(~500/istek ≈ 40 çağrı) dakikalar içinde (SC-004)

**Constraints**: Storefront açılışta OpenAI ApiKey ister (fail-fast, ChatAgent emsali) — user-secrets
gerekir; storefront MCP anonim KALIR (061 kararı); embedding API kesintisi event retry'ına düşer

**Scale/Scope**: ~20k ürün, 1536 boyutlu vektör satır başına ~6KB JSONB; tek BC + ChatAgent config

## Constitution Check

*GATE: Phase 0 öncesi geçti; Phase 1 tasarımı sonrası yeniden değerlendirildi — İHLAL YOK.*

| İlke | Durum | Değerlendirme |
|---|---|---|
| I — BC izolasyonu | PASS | Her şey Storefront BC içinde; yeni cross-BC kanal/event yok (`ProductChangedEvent` zaten Description taşır). OpenAI dış AI hizmetidir, BC değil (ChatAgent/ModerationAgent emsali). MCP'yi yalnız agent tüketir. |
| II — Zengin aggregate | PASS | Yeni aggregate yok; `StorefrontView` read-model'dir (aggregate değil, mevcut sanksiyonlu istisna). Embedding alanı read-model yaşam döngüsüne uyar. |
| III — VSA + CQRS | PASS | Yeni işler `Features/Agents/*` slice'ları (izole, kendi handler'ı — agent-slice konvansiyonu); repository yok, doğrudan `IDocumentSession`; backfill `BackgroundService` (yeni REST endpoint kontratı yok). |
| IV — Result pattern | PASS | Handler'lar `Feature*ResultModel` döner; "bulunamadı" beklenen durumdur — hata değil, boş-sonuç işaretli Ok (`Found=false` alanı), exception yok. |
| V — Scope yetki | PASS | Yeni scope yok; storefront MCP anonim kalır (anayasa: anonim keşif meşru; 061 kararıyla tutarlı). Backfill'in dış yüzeyi yok (BackgroundService) → yetki yüzeyi doğmaz. |
| VI — Domain-TDD | PASS | Saf karar mantığı (`yeniden-embedding gerekir mi`: eski/yeni açıklama + mevcut embedding durumu) test-first yazılır; handler/SQL/BackgroundService kapsam dışı (test-sonra/canlı). |
| VII — FLOW.md | PASS | Storefront domain süreci değişiyor (yeni policy: açıklama değişti → anlamsal temsil tazelenir; backfill süreci) → `src/services/storefront/FLOW.md` AYNI PR'da güncellenir. |

## Project Structure

### Documentation (this feature)

```text
specs/067-storefront-semantic-search/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 — kararlar + doğrulanmış API bulguları
├── data-model.md        # Phase 1 — StorefrontView delta + options
├── quickstart.md        # Phase 1 — canlı doğrulama senaryoları
├── contracts/
│   └── mcp-tools.md     # Phase 1 — 5 MCP tool kontratı
└── tasks.md             # /speckit-tasks üretir (bu komut DEĞİL)
```

### Source Code (repository root)

```text
src/services/storefront/Storefront.Api/
├── Domains/StorefrontView/
│   ├── StorefrontView.cs                        # + DecideEmbedding karar yardımcısı (saf)
│   ├── ProductDescriptionEmbedding.cs           # YENİ: temsil yol-arkadaşı dokümanı (R1 revizyonu)
│   ├── StorefrontMcpTools.cs                    # + 4 yeni tool sarmalayıcısı (ince)
│   └── Features/Agents/
│       ├── SearchStorefrontProducts.cs          # GENİŞLER: category/publisher/exclude*/semanticQuery
│       ├── FindSimilarBooks.cs                  # YENİ: ürünün kendi embedding'iyle kNN (OpenAI çağrısı yok)
│       ├── ListCategoriesForAgent.cs            # YENİ: satılabilir kümeden distinct + sayı
│       ├── ListAuthorsForAgent.cs               # YENİ: search + maxResults (binlerce yazar)
│       └── ListPublishersForAgent.cs            # YENİ
├── Options/
│   ├── OpenAiOption.cs                          # YENİ: ApiKey (Required) + EmbeddingModel
│   └── SemanticSearchOption.cs                  # YENİ: MaxCosineDistance, BackfillBatchSize
├── EmbeddingBackfillService.cs                  # YENİ: startup BackgroundService (idempotent tarama)
├── StorefrontEventHandlers.cs                   # ProductChangedEvent: embedding üretimi eklenir
├── Program.cs                                   # UsePgVector() + IEmbeddingGenerator singleton + options
└── FLOW.md                                      # süreç güncellemesi (İLKE VII, aynı PR)

src/agents/ChatAgent/ConstValues.cs              # StorefrontTools sabitleri + public/assistant allowlist + prompt
tests/Storefront.Api.Tests/                      # NeedsReembedding birim testleri (test-first)
CLAUDE.md                                        # OpenAI fail-fast listesine Storefront + BC haritası satırı
```

**Structure Decision**: Yeni proje yok. Tüm arama/keşif slice'ları Storefront'ta (satılabilir/yayında
kümenin sahibi zaten o — brainstorm kararı). ChatAgent yalnız config/prompt düzeyinde dokunulur.
Catalog'a dokunulmaz (embedding read-model verisidir, `Description` event'te zaten akıyor).

## Complexity Tracking

İhlal yok — tablo boş.