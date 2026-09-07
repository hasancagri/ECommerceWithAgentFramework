# Tasks: Storefront Semantic Search

**Input**: Design documents from `/specs/067-storefront-semantic-search/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/mcp-tools.md, quickstart.md

**Tests**: İLKE VI — saf domain birimi (`NeedsReembedding` karar mantığı) test-first ZORUNLU (T006→T007).
Handler/SQL/BackgroundService/MCP test-sonra veya canlı doğrulama.

**Organization**: Faz sırası US3 → US1 → US2. İkisi de P1; US3 öne alındı çünkü embedding
altyapısından ve açıklama verisinden BAĞIMSIZ — hemen canlı doğrulanır (US1/US2 semantik doğrulaması
description'lı reseed bekler, spec Assumption).

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [x] T001 `src/services/storefront/Storefront.Api/Storefront.Api.csproj`'a sürümsüz PackageReference: `Marten.PgVector`, `Pgvector`, `Microsoft.Extensions.AI.OpenAI` (sürümler zaten `Directory.Packages.props`'ta pinli); gerekli namespace'ler `GlobalUsings.cs`'e
- [x] T002 Dev secret: `dotnet user-secrets set OpenAI:ApiKey <k> --project src/services/storefront/Storefront.Api/Storefront.Api.csproj` (fail-fast açılış için ön koşul; anahtar kullanıcıdan istenir)

---

## Phase 2: Foundational — embedding altyapısı

**Purpose**: US1 + US2'yi bloklar. **US3 bu faza BAĞIMLI DEĞİL** (Phase 3 önce koşulabilir).

- [x] T003 [P] `src/services/storefront/Storefront.Api/Options/OpenAiOption.cs`: `ApiKey [Required]` + `EmbeddingModel` (default `text-embedding-3-small`) — data-model.md tablosu
- [x] T004 [P] `src/services/storefront/Storefront.Api/Options/SemanticSearchOption.cs`: `MaxCosineDistance` (0.55) + `BackfillBatchSize` (500)
- [x] T005 `src/services/storefront/Storefront.Api/Program.cs`: iki options kaydı (`BindConfiguration` + `ValidateOnStart`), `opts.UsePgVector()`, `VectorOn<StorefrontView>(..., 1536, Cosine)` denemesi (research R7 — kurulamazsa exact scan, karar log'lanır), `IEmbeddingGenerator<string, Embedding<float>>` singleton (`OpenAIClient(ApiKey).GetEmbeddingClient(Model).AsIEmbeddingGenerator()`)
- [x] T006 **TEST-FIRST (İLKE VI)** `tests/Storefront.Api.Tests/StorefrontViewReembeddingTests.cs`: data-model.md karar tablosunun 4 satırı + boş/null açıklama kombinasyonları — FAILING testler önce yazılır
- [x] T007 `src/services/storefront/Storefront.Api/Domains/StorefrontView/StorefrontView.cs`: `DescriptionEmbedding: float[]?` alanı + `NeedsReembedding` saf karar yardımcısı — T006 testlerini geçir
- [x] T008 `src/services/storefront/Storefront.Api/StorefrontEventHandlers.cs`: ProductChangedEvent handler'ında `ApplyCatalog` öncesi eski Description yakala; `NeedsReembedding` kararına göre embedding üret/null'a çek (IsDeleted etkilemez — research R4); hata exception→Wolverine retry
- [x] T009 `src/services/storefront/Storefront.Api/EmbeddingBackfillService.cs`: startup `BackgroundService` — Description dolu + embedding null satırları `BackfillBatchSize`'lık batch'lerle doldur, kalan 0'a dek döngü, concurrency çakışmasında satırı atla (research R6); `Program.cs`'e `AddHostedService`

**Checkpoint**: `dotnet build` yeşil; Storefront ApiKey'siz açılışta fail-fast; T006 testleri yeşil.

---

## Phase 3: US3 — Kategori/yazar/yayınevi keşfi (P1) 🎯 İlk canlı değer

**Goal**: "Hangi kategoriler/yazarlar/yayınevleri var" chat'te yanıtlanır (FR-006, SC-003).

**Independent Test**: quickstart S1 — Phase 2'siz ve açıklama verisisiz koşar.

- [x] T010 [P] [US3] `src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Agents/ListCategoriesForAgent.cs`: satılabilir kümeden distinct kategori + ProductCount; `[Cached("filters", 60)]`; izole handler (facet query REUSE edilmez — agent-slice konvansiyonu)
- [x] T011 [P] [US3] `.../Features/Agents/ListAuthorsForAgent.cs`: `search` (default "") + `maxResults` (default 50, 1–200) + `TotalCount`; ProductCount DESC (kontrat #4); `[Cached("filters", 60)]` (parametreli — cache anahtarı mesaj bazlı, aynı etiketle invalidasyon)
- [x] T012 [P] [US3] `.../Features/Agents/ListPublishersForAgent.cs`: `search` + `maxResults` (default 100) + `TotalCount`; ProductCount DESC (kontrat #5); `[Cached("filters", 60)]`
- [x] T013 [US3] `.../Domains/StorefrontView/StorefrontMcpTools.cs`: `list_categories` / `list_authors` / `list_publishers` ince sarmalayıcıları — HER opsiyonel parametreye default (tuzak: `mcp-tool-optional-param-default`)
- [x] T014 [US3] `src/agents/ChatAgent/ConstValues.cs`: `StorefrontTools`'a 3 sabit; public + assistant allowlist'lerine ekle; prompt'a keşif davranışı ("hangi kategoriler var" → list tool'ları; sonuçtan kategoriye dalma örneği)
- [x] T015 [US3] Canlı doğrulama (quickstart S1): üç soru + yayından-kaldırma negatif kontrolü; Aspire'dan başlat

**Checkpoint**: US3 tek başına teslim edilebilir MVP dilimi.

---

## Phase 4: US1 — Bulanık/temalı hibrit arama (P1)

**Goal**: Yapısal kısıt + tema tek cümlede; yapısal ÖNCE, kNN SONRA, eşik dürüstlüğü (FR-001..003, FR-005, FR-008).

**Independent Test**: quickstart S3/S4; semantik kısım description'lı reseed ister — yapısal kısım (S4) hemen.

- [x] T016 [US1] `.../Features/Agents/SearchStorefrontProducts.cs`: yeni yapısal parametreler — `category`, `publisher`, `excludeAuthors`, `excludePublishers` (hepsi default'lu, case-insensitive; kontrat #1); `semanticQuery` boşken mevcut Name ASC davranışı korunur; response'a `Found`
- [x] T017 [US1] Aynı slice'a `semanticQuery` yolu: `IEmbeddingGenerator` ile sorgu vektörü (1 çağrı) → LINQ yapısal ön-filtre + `DescriptionEmbedding != null` → `ProductId` listesi → parametreli ham SQL kNN (`(data->'DescriptionEmbedding')::vector <=> :q < :threshold order by ... limit :n`, research R2); eşik altı boş → `Found=false`
- [x] T018 [US1] `StorefrontMcpTools.cs`: `search_storefront_products` sarmalayıcısına yeni parametreler + default'lar (kontrat #1)
- [x] T019 [US1] `src/agents/ChatAgent/ConstValues.cs` prompt: ayrıştırma sözleşmesi (yapısal→parametre, tema→`semanticQuery`; ham cümle tool'a gitmez), tür/tema ifadesi kriter SAYILIR (kriter-dilenme gevşer — bulgu #2), `Found=false`→dürüst "bulunamadı", cross-facet VEYA = çoklu çağrı + birleştir
- [ ] T020 [US1] Canlı doğrulama: S4 (yapısal — hemen); description verisi varsa S2 (backfill: `kalan=0` SQL kontrolü + ikinci restart no-op) + S3 (SC-001 kısıt kontrolü + FR-007 negatif adımı: sonuçtaki kitabı yayından kaldır → aynı sorguda düşer) + S5.1 (alakasız sorgu → "bulunamadı", SC-005 arama yolu) + JSON casing doğrulaması (`data->'DescriptionEmbedding'` psql'de dolu — research R9)

**Checkpoint**: Yapısal genişleme canlı; semantik yol veri gelince S2/S3 ile kapanır.

---

## Phase 5: US2 — Buna benzer kitaplar (P2)

**Goal**: "Buna benzer ne var" — ürünün kendi embedding'iyle kNN, kendisi hariç (FR-004, SC-002).

**Independent Test**: quickstart S5.2–S5.3; description verisi ister.

- [x] T021 [US2] `.../Features/Agents/FindSimilarBooks.cs`: `productId` (zorunlu) + `maxResults` (default 8); ürünün embedding'ini DB'den oku (OpenAI çağrısı YOK), T017'deki iki-adım SQL desenini kendi handler'ında taşı (bilinçli tekrar — agent slice izolasyonu), `id != :self` + eşik; ürün yok/embedding null → `Found=false` + mesaj kodu (`StorefrontResourceConstants`'a yeni sabit)
- [x] T022 [US2] `StorefrontMcpTools.cs`: `find_similar_books` sarmalayıcısı (kontrat #2); `ConstValues.cs`: sabit + iki allowlist + prompt ("buna benzer" örneği, boşta "benzer bulunamadı")
- [ ] T023 [US2] Canlı doğrulama (quickstart S5.2–S5.3): benzer listesi self-hariç + yayından-kaldırılan benzerlerde görünmez (FR-007), embedding'siz ürün kibar boş (SC-002); S5.1 US1 kapsamında (T020)

---

## Phase 6: Polish & Cross-Cutting

- [x] T024 [P] `src/services/storefront/FLOW.md`: yeni policy adımları — "açıklama değişti → anlamsal temsil tazelenir (StorefrontView.NeedsReembedding → embedding)" + backfill süreci + semantik arama sınırı; kenar-anchor tip adlarıyla (İLKE VII, aynı PR)
- [x] T025 [P] `CLAUDE.md`: OpenAI fail-fast listesine Storefront.Api; BC haritası storefront satırına semantik arama + keşif tool'ları; gerekirse 067 spec yolu
- [x] T026 Eşik kalibrasyonu (quickstart S5.4): hem arama (metin→ürün) hem benzer-kitap (ürün→ürün) örnekleriyle `MaxCosineDistance` ayarla — iki yolun mesafe dağılımı ayrışırsa ikinci eşik alanı aç (şimdilik tek değer); description verisi yoksa 0.55 kalır + kalibrasyon memory'ye AÇIK not düşülür
- [x] T027 Kapanış: `dotnet build` + `dotnet test` (tüm çözüm) + `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` hepsi yeşil

---

## Dependencies

- Phase 1 → Phase 2 → US1(Phase 4) → US2(Phase 5): zincir. T006 → T007 (test-first, İLKE VI).
- **US3(Phase 3) yalnız T001'e gevşek bağlı** (paket eklemeden de derlenir) — Phase 2'den ÖNCE koşulabilir.
- US2, US1'in T017 desenini örnek alır ama koduna bağımlı değil (bilinçli tekrar) — Phase 2 sonrası
  teknik olarak bağımsız; sıra prompt/allowlist çakışmasını (ConstValues) seri tutmak için.
- `ConstValues.cs`'e dokunan T014/T019/T022 SERİ (aynı dosya).

## Parallel Examples

- Phase 2: T003 ‖ T004 (farklı dosyalar); T006 testleri T005 ile paralel yazılabilir.
- Phase 3: T010 ‖ T011 ‖ T012 (üç ayrı slice dosyası).
- Phase 6: T024 ‖ T025.

## Implementation Strategy

- **MVP = Phase 3 (US3)**: embedding'siz, veri beklemeden bugün canlı değer (bilinen keşif kırığı kapanır).
- Sonra Phase 1+2 altyapı, Phase 4 yapısal genişleme (S4 hemen doğrulanır); semantik doğrulama (S2/S3/S5)
  description'lı reseed'e paralel — feature'ın bitmişliği veri işine rehin değil.
- Her checkpoint'te dur-doğrula; canlı doğrulamalar hep Aspire AppHost'tan.