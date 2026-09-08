# Implementation Plan: Query Storefront — Tek Serbest-Sorgu Kapısı

**Branch**: `069-query-storefront` | **Date**: 2026-09-07 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/069-query-storefront/spec.md`

## Summary

Storefront'un agent-sorgu yüzeyi tek MCP tool'a iner: `query_storefront(sql)`. LLM, salt-okur
`storefront_sellable` Postgres VIEW'ına (StorefrontView + embedding sidecar JOIN'i, satılabilirlik
filtresi gömülü) SQL yazar; bekçi (saf, test-first `AgentSqlGuard`) çalıştırma öncesi denetler;
`{{EMBED:"metin"}}` yer-tutucusunu sistem embedding'e çevirip bind eder; kısıtlı DB rolü + LIMIT
tavanı + statement_timeout yapısal zırhtır; her sorgu `AgentQueryLog` dokümanına izlenir.
`search_storefront_products` + `find_similar_books` tam ikameyle SİLİNİR; ChatAgent prompt'u şema +
sorgu kalıplarıyla (kNN, benzerlik, sayfalama, düzeltme döngüsü) baştan yazılır. Eval seti +
şema-prompt drift guard'ı kalite ağıdır.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings`)

**Primary Dependencies**: Marten (Postgres document store, Newtonsoft, `UsePgVector()`), Npgsql
(ham SQL + ayrı kısıtlı DataSource), Wolverine (`IMessageBus`), MCP server (`WithToolsFromAssembly`),
`Microsoft.Extensions.AI.OpenAI` (`IEmbeddingGenerator`, 067'den mevcut), ChatAgent (MAF, tüketici)

**Storage**: storefrontDb — mevcut `mt_doc_storefrontview` + `mt_doc_productdescriptionembedding`
üstüne YENİ `storefront_sellable` VIEW; YENİ `AgentQueryLog` Marten dokümanı; YENİ kısıtlı DB rolü
(SELECT yalnız view). Şema değişikliği startup bootstrap ile (research R1)

**Testing**: xUnit + Shouldly (`tests/Storefront.Api.Tests`); test-first: `AgentSqlGuard`,
`{{EMBED}}` parser, LIMIT sarmalama (İLKE VI). Handler/MCP/prompt = canlı doğrulama + eval seti

**Target Platform**: Aspire AppHost altında Linux/macOS servis (mevcut topoloji)

**Project Type**: Mevcut mikroservis (Storefront.Api) + ChatAgent prompt/allowlist değişikliği

**Performance Goals**: Sorgu yanıtı timeout tavanı 3 sn (statement_timeout); 20k satır exact
vektör taraması ms-mertebesi (mevcut kanıt); embedding çağrısı yalnız `{{EMBED}}` varsa (+1 API turu)

**Constraints**: Satır tavanı 50 (`MaxRows`, sarmalanmış LIMIT); SQL uzunluk tavanı; tek SELECT/WITH;
yalnız `storefront_sellable` ilişkisi; asistan düzeltme hakkı ≤2; MCP anonim kalır (061 duruşu)

**Scale/Scope**: ~20k kitap, tek view; 2 tool silinir → 1 tool girer; ChatAgent 2 prompt bloğu
yeniden yazılır; eval ~25 soru / 13 sınıf

## Constitution Check

*GATE: Phase 0 öncesi geçildi; Phase 1 sonrası yeniden değerlendirildi — İHLAL YOK.*

| İlke | Değerlendirme | Sonuç |
|---|---|---|
| I — BC izolasyonu | Kapsam yalnız Storefront + tüketici ChatAgent. View/rol kendi DB'sinde; başka BC verisi yok. MCP'yi yalnız agent tüketir (tool → `Features/Agents` slice). `UserPurchase` yüzeyin DIŞINDA (rol SELECT'i yalnız view'a — yapısal). | PASS |
| II — Zengin aggregate | Yeni aggregate YOK. `AgentQueryLog` = davranışsız iz dokümanı (read-model/iz istisnası, aggregate değil); `StorefrontView` zaten read-model. | PASS |
| III — VSA + CQRS | Yeni slice `Features/Agents/QueryStorefront.cs` (agent slice izole, bilinçli tekrar kuralı). Repository yok — handler Npgsql/`IDocumentSession` doğrudan. MCP tool ince sarmalayıcı. | PASS |
| IV — Result pattern | Ret/hata `FeatureObjectResultModel<T>` + `StorefrontResourceConstants` kodlarıyla (makine-okur, FR-007). Exception yalnız beklenmedik. | PASS |
| V — Scope yetki | Anonim okuma yüzeyi meşru (anayasa: "anonim gezinme meşrudur"); scope değişikliği yok; storefront MCP anonim kalır (061). | PASS |
| VI — Domain-TDD | Saf birimler (guard, EMBED parser, LIMIT sarmalayıcı) test-first; test task'ları implementasyondan önce. Handler/prompt kapsam dışı. | PASS |
| VII — FLOW.md | Domain süreci değişiyor (arama adımları → tek sorgu kapısı adımı) → `src/services/storefront/FLOW.md` AYNI PR'da güncellenir; anchor guard koşulur. | PASS (plan yükümlülüğü) |

Not — bilinçli tasarım sapması (anayasa DIŞI, memory kaydı): "LLM SQL yazmaz" proje kararı
(`text-first-discovery-design-decisions` #1) Storefront'a sınırlı ters çevrildi; itirazlar +
kabul edilen riskler spec Assumptions'ta. Anayasa ilkesi ihlali değildir; Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/069-query-storefront/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 — R1..R9 kararları
├── data-model.md        # Phase 1 — view kolonları, AgentQueryLog, silinenler
├── quickstart.md        # Phase 1 — canlı doğrulama + güvenlik probları
├── contracts/
│   ├── query-storefront-tool.md   # MCP tool + bekçi + hata kodları sözleşmesi
│   └── eval-set.md                # FR-008 soru sınıfları + beklenen desenler
└── tasks.md             # /speckit-tasks üretir (bu komut DEĞİL)
```

### Source Code (repository root)

```text
src/services/storefront/Storefront.Api/
├── AgentSql/                          # YENİ — sorgu kapısının saf çekirdeği + altyapısı
│   ├── StorefrontSellableSchema.cs    # TEK KAYNAK: view kolon listesi (ad+tip+açıklama) → DDL + drift guard
│   ├── AgentSqlGuard.cs               # saf bekçi: tek SELECT/WITH, ilişki whitelist, yasak kelime, uzunluk (test-first)
│   ├── EmbedPlaceholder.cs            # saf {{EMBED:"..."}} ayrıştırıcı + parametre ikamesi (test-first)
│   ├── AgentQueryLog.cs               # iz dokümanı (Marten)
│   └── AgentQuerySurfaceBootstrap.cs  # startup: CREATE OR REPLACE VIEW + rol + GRANT (idempotent)
├── Options/AgentQueryOption.cs        # YENİ — MaxRows, TimeoutSeconds, MaxSqlLength, rol kimlik bilgisi
├── Domains/StorefrontView/
│   ├── StorefrontMcpTools.cs          # DEĞİŞİR — 2 tool silinir, query_storefront girer
│   └── Features/Agents/
│       ├── QueryStorefront.cs         # YENİ slice — guard→embed→kısıtlı bağlantıda çalıştır→logla
│       ├── SearchStorefrontProducts.cs  # SİLİNİR
│       └── FindSimilarBooks.cs          # SİLİNİR
├── ProductEmbeddingKnnQuery.cs        # SİLİNİR (vektör-literal yardımcı AgentSql'e taşınır)
├── Options/SemanticSearchOption.cs    # SİLİNİR (eşik prompt kalıbına gömülür — research R7)
└── FLOW.md                            # GÜNCELLENİR (İLKE VII, aynı PR)

src/agents/ChatAgent/
├── ConstValues.cs                     # StorefrontTools → tek sabit; iki prompt bloğu yeniden yazılır
└── Program.cs                         # public + assistant allowlist güncellenir

tests/Storefront.Api.Tests/            # AgentSqlGuardTests, EmbedPlaceholderTests, LimitWrapTests (test-first)
scripts/check-agent-query-schema.sh    # YENİ drift guard: şema tek-kaynağı ↔ ChatAgent prompt
```

**Structure Decision**: Mevcut Storefront.Api içinde kalınır; yeni servis/proje YOK. Saf çekirdek
`AgentSql/` klasöründe (kök-seviye teknik yerleşim emsali: `ProductEmbeddingKnnQuery.cs`);
slice VSA gereği `Features/Agents/` altında. Catalog `list_*` ve iç okuma yüzeylerine dokunulmaz (FR-009).

## Complexity Tracking

İhlal yok — tablo boş.