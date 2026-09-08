# Tasks: Query Storefront — Tek Serbest-Sorgu Kapısı

**Input**: Design documents from `/specs/069-query-storefront/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: İLKE VI — saf birimler (AgentSqlGuard, EmbedPlaceholder) test-first ZORUNLU; test task'ı
implementasyondan önce. Handler/MCP/prompt/bootstrap = canlı doğrulama (quickstart + eval).

**Organization**: Faz sırası US3 → US1 → US2 (üçü de P1; spec: "bu güvenceler olmadan US1 yayına
çıkamaz" — güvenli boru hattı önce kurulur, sözleşme takası sonra, anlamsal katman en son).

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

**Purpose**: Options + hata kodları — her fazın ortak zemini

- [X] T001 `AgentQueryOption` POCO (`MaxRows=50`, `TimeoutSeconds=3`, `MaxSqlLength=4000`, `RoleName`/`RolePassword` `[Required]`) + `AddOptions<T>().BindConfiguration(nameof(AgentQueryOption)).ValidateDataAnnotations().ValidateOnStart()` kaydı — `src/services/storefront/Storefront.Api/Options/AgentQueryOption.cs` + `Program.cs`
- [X] T002 [P] `AgentSql*` ret/hata kodu sabitleri (kontrattaki 9 kod: MultiStatement, NotReadOnly, ForbiddenKeyword, UnknownRelation, TooLong, BadEmbedPlaceholder, PermissionDenied, Timeout, ExecutionFailed) — `src/services/storefront/Storefront.Api/Constants/StorefrontResourceConstants.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: İzinli vitrin yüzeyi (view + rol) + iz dokümanı — TÜM story'ler buna muhtaç

**⚠️ CRITICAL**: Bu faz bitmeden story işi başlayamaz

- [X] T003 `StorefrontSellableSchema` TEK-KAYNAK kolon listesi (ad + PG tipi + jsonb kaynak ifadesi + tek satır açıklama; data-model.md tablosu birebir) + bu listeden `CREATE OR REPLACE VIEW` DDL üretimi — `src/services/storefront/Storefront.Api/AgentSql/StorefrontSellableSchema.cs`
- [X] T004 `AgentQuerySurfaceBootstrap` hosted service: idempotent view DDL + kısıtlı rol (yoksa oluştur, `LOGIN PASSWORD` option'dan) + `GRANT USAGE ON SCHEMA` + yalnız view'a `GRANT SELECT`; `AddMarten` SONRASI kayıt (research R1) + kısıtlı `NpgsqlDataSource` (storefrontDb conn-string'i rol kimliğiyle) DI kaydı — `src/services/storefront/Storefront.Api/AgentSql/AgentQuerySurfaceBootstrap.cs` + `Program.cs`
- [X] T005 [P] `AgentQueryLog` dokümanı (data-model.md §2 alanları) + Marten şema kaydı — `src/services/storefront/Storefront.Api/AgentSql/AgentQueryLog.cs` + `Program.cs`

**Checkpoint**: Aspire açılışında view + rol kurulur (quickstart Q1 elle koşulabilir)

---

## Phase 3: User Story 3 — Sorgu yüzeyi güvenli ve gözlemlenebilir (Priority: P1)

**Goal**: Bekçi + kısıtlı rol + tavan/timeout + iz — serbest-sorgu kapısının güvenli boru hattı

**Independent Test**: quickstart Q1 + Q2 — kötücül/aykırı sorgular (mutasyon, view-dışı ilişki,
çoklu-statement, tavansız, uzun-süren) MCP'ye gönderilir; hepsi çalışmadan ret + `AgentQueryLog` izli

- [X] T006 [US3] TEST-FIRST: `AgentSqlGuard` birim testleri — yorum soyma, `;` çoklu-statement reddi, SELECT/WITH-dışı reddi, yasak kelime listesi (KELİME-SINIRLI: `OFFSET 50` GEÇER; string-literal DIŞI: `ILIKE '%drop%'` GEÇER), FROM/JOIN whitelist (`storefront_sellable` dışı ret; POZİTİF: `FROM unnest(authors)` + CTE alias'ı FROM'da GEÇER), uzunluk tavanı, LIMIT sarmalama (`MaxRows+1`) + geçerli sorguların GEÇMESİ — `tests/Storefront.Api.Tests/AgentSqlGuardTests.cs` (KIRMIZI koşulur)
- [X] T007 [US3] `AgentSqlGuard` saf implementasyon: `ResultDomain<GuardedQuery>` (WrappedSql + ret kodu `MessageItem`); T006 YEŞİL — `src/services/storefront/Storefront.Api/AgentSql/AgentSqlGuard.cs`
- [X] T008 [US3] `QueryStorefrontForAgent` slice'ı: guard → kısıtlı DataSource'ta `SET LOCAL statement_timeout` ile çalıştır → DataReader'dan kolon-adlı `Rows` (vector kolon AYIKLA, R5) → `Truncated` tespiti → ret DAHİL her yolda `AgentQueryLog` yaz (sahip session) → `FeatureObjectResultModel<Response>` — `src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Agents/QueryStorefront.cs`
- [X] T009 [US3] `query_storefront` MCP tool ince sarmalayıcısı (tek zorunlu `sql` parametresi, kontrat şekli) — `src/services/storefront/Storefront.Api/Domains/StorefrontView/StorefrontMcpTools.cs`
- [X] T010 [US3] Canlı doğrulama: quickstart Q1 (view/rol yapısal kanıt, idempotent restart) + Q2 (güvenlik probları + log sorgusu) — 2026-09-08 canlı PASS (7 prob + 7 iz satırı)

**Checkpoint**: Kapı güvenli — SC-003/SC-004/SC-005 kanıtlı; eski tool'lar hâlâ yerinde (takas sonraki faz)

---

## Phase 4: User Story 1 — Hiçbir soru duvara çarpmaz (Priority: P1)

**Goal**: Sözleşme takası — eski 2 tool silinir, ChatAgent tek kapıya bağlanır, yeni soru sınıfları açılır

**Independent Test**: quickstart Q4 — istatistik/karşılaştırma/cross-facet VEYA soruları chat'ten
sorulur; gerçek veriden, uydurmasız yanıt; "parametreye sığmadı" reddi sınıfı yok

- [X] T011 [US1] TAM İKAME silme: `Features/Agents/SearchStorefrontProducts.cs` + `Features/Agents/FindSimilarBooks.cs` + `StorefrontMcpTools.cs` içindeki 2 eski sarmalayıcı + `ProductEmbeddingKnnQuery.cs` + `Options/SemanticSearchOption.cs` — kopya YOK; `ToVectorLiteral` mantığı T017'de `EmbedPlaceholder` içinde yeniden yazılır (git geçmişinden) — `src/services/storefront/Storefront.Api/`
- [X] T012 [US1] ChatAgent sabitleri + prompt yeniden yazımı: `StorefrontTools` → tek `QueryStorefront` sabiti; `PublicInstructions` + `AssistantInstructions` storefront blokları → şema bloğu (T003 kolonlarıyla birebir) + sorgu kalıpları (ILIKE/unnest ad eşleşmesi, `specs` jsonb özellik + `family_code` varyant kalıbı — eval G, sayfalama `Truncated`→"devamını göstereyim mi", düzeltme ≤2 deneme, dürüst veri sınırı: satış adedi yok / `added_at` yaklaşık, grounding) — `src/agents/ChatAgent/ConstValues.cs`
- [X] T013 [US1] ChatAgent allowlist'leri: publicAgentTools + assistantAgentTools'ta 2 eski tool adı → `query_storefront`; Catalog `list_*` DOKUNULMAZ — `src/agents/ChatAgent/Program.cs`
- [X] T014 [P] [US1] Drift guard script'i: `StorefrontSellableSchema.cs`'teki her kolon adının `ConstValues.cs`'te geçtiğini doğrular (emsal `check-flow-links.sh`; çalıştırılabilir bit; BİLİNÇLİ SINIR: tek yönlü — prompt'taki bayat kolonu yakalamaz, o review disiplini) — `scripts/check-agent-query-schema.sh`
- [X] T015 [US1] Canlı doğrulama: quickstart Q4 (yeni soru sınıfları) + eski tool adlarının MCP listesinde OLMADIĞI (FR-001) — 2026-09-08 PASS (tools/list yalnız query_storefront; istatistik/karşılaştırma/VEYA gerçek veriden)

**Checkpoint**: Tek kapı canlı; yapısal soru uzayı tam — anlamsal yol henüz kapalı (Phase 5)

---

## Phase 5: User Story 2 — Temalı arama ve benzerlik aynı kapıdan (Priority: P1)

**Goal**: `{{EMBED}}` yer-tutucusu + kNN/benzerlik kalıpları — 067 paritesi + filtreli-benzerlik

**Independent Test**: quickstart Q3 — 067 S3/S5 senaryoları yeni kapıda geçer + "benzer AMA 200 TL
altı ve stokta" yeni senaryosu

- [X] T016 [US2] TEST-FIRST: `EmbedPlaceholder` birim testleri — `{{EMBED:"metin"}}` ayrıştırma (tek/çoklu/kaçışlı tırnak), parametre ikamesi (`CAST(@embN AS vector)`), bozuk sözdizimi reddi (`AgentSqlBadEmbedPlaceholder`), vektör-literal üretimi (InvariantCulture) — `tests/Storefront.Api.Tests/EmbedPlaceholderTests.cs` (KIRMIZI koşulur)
- [X] T017 [US2] `EmbedPlaceholder` saf implementasyon (+ `ToVectorLiteral` buraya taşınır); T016 YEŞİL — `src/services/storefront/Storefront.Api/AgentSql/EmbedPlaceholder.cs`
- [X] T018 [US2] Slice'a EMBED akışı: ayrıştır → ikameli SQL bekçiden → GEÇERSE `IEmbeddingGenerator` (067 singleton) ile embed → metin-literal + CAST bind → çalıştır; log'a HAM (ikamesiz) SQL (R3/R6) — `src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Agents/QueryStorefront.cs`
- [X] T019 [US2] Prompt anlamsal kalıpları: `{{EMBED}}` + eşik 0.68 + eşik-altı "bulunamadı"; benzerlik alt-sorgusu (`product_id <> 'X'` kendisi-hariç) + yapısal kısıtla birleşim — `src/agents/ChatAgent/ConstValues.cs`
- [X] T020 [US2] Canlı doğrulama: quickstart Q3 (067 S3/S5 regresyonu + filtreli-benzerlik + yayından-kaldırma yolu) — 2026-09-08 PASS (S3=H, S5=M, filtreli-benzerlik=I kendisi-hariç; yayından-kaldırma Q1'de tx kanıtı)

**Checkpoint**: SC-002 kanıtlı — anlamsal yetenekte sıfır gerileme + yeni filtreli-benzerlik

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Belge senkronu + eval + bütünlük

- [X] T021 [P] `src/services/storefront/FLOW.md`: arama/benzerlik adımları → "tek sorgu kapısı" süreci (bekçi→embed→çalıştır→izle, kenar-anchor'larla: `AgentSqlGuard`, `QueryStorefrontForAgent`, `AgentQueryLog`) — İLKE VII, aynı PR
- [X] T022 [P] `CLAUDE.md` storefront satırı: parametrik arama + keşif tool anlatımı → `query_storefront` (view+bekçi+iz) — BC haritası güncel
- [X] T023 Eval koşumu: `contracts/eval-set.md` A–M sınıfları chat'ten (quickstart Q5) — 2026-09-08: 13/13 sınıf PASS (SC-001 %100); kalibrasyon prompt'a işlendi (İngilizce katalog çevirisi, soyad-parça ILIKE, COUNT+sayfa kalıbı, benzerlik tek-sorgu)
- [X] T024 Bütünlük: `dotnet build` + `dotnet test tests/Storefront.Api.Tests/Storefront.Api.Tests.csproj` + `scripts/check-agent-query-schema.sh` + `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` (quickstart Q6, hepsi yeşil)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 → 2**: T001 (option) T004'ün, T002 (kodlar) T007/T008'in girdisi
- **Phase 2 → 3**: view+rol+log olmadan slice çalışamaz
- **Phase 3 → 4**: takas, güvenli kapı kanıtlanmadan yapılmaz (spec: US3 = yayın ön şartı)
- **Phase 4 → 5**: T018/T019, T008 slice'ını ve T012 prompt'unu değiştirir (aynı dosyalar)
- **Phase 6**: tümü sonrası; T023 (eval) anlamsal sınıflar (H/I) için Phase 5'i bekler

### Story bağımsız-test sırası

US3 quickstart Q1+Q2 ile Phase 3 sonunda; US1 Q4 ile Phase 4 sonunda; US2 Q3 ile Phase 5 sonunda —
her checkpoint kendi başına doğrulanabilir.

### Parallel Opportunities

- T001 ‖ T002 (farklı dosyalar)
- T005 ‖ T004 (log dokümanı bootstrap'tan bağımsız; ikisi de Program.cs'e dokunur — kayıt satırları ayrı, sıra önemsiz)
- T014 (script) ‖ T012/T013 (ChatAgent dosyaları) — farklı dosyalar
- T021 ‖ T022 (FLOW.md / CLAUDE.md)
- Test-first çiftleri sıralı: T006→T007, T016→T017 (İLKE VI)

---

## Implementation Strategy

1. **Güvenli çekirdek önce** (Phase 1-3): kapı + zırh + iz — eski tool'lar bozulmadan yaşar; her an durulabilir.
2. **Takas tek hamlede** (Phase 4): silme + prompt + allowlist aynı fazda — yarım-takas ara durumu PR içinde kalır.
3. **Anlamsal katman** (Phase 5): EMBED + kalıplar; 067 regresyonu burada kanıtlanır.
4. **Eval en sonda** (Phase 6): tüm soru sınıfları açıkken ölçülür; %90 altı kalırsa prompt iterasyonu T023 içinde.

MVP kesiti: Phase 1-4 (güvenli kapı + tam yapısal soru uzayı); Phase 5 olmadan yayın ÖNERİLMEZ
(SC-002 gerileme — 067 canlı yeteneği kaybolur). Pratik teslim = tüm fazlar tek PR.

---

## Notes

- Prompt/allowlist değişen her task sonrası ChatAgent restart (Aspire) gerekir — canlı doğrulamalar bunu varsayar.
- T011 sonrası `dotnet build` KIRIK olabilir (ChatAgent sabitleri eski adları referanslıyorsa) — T012/T013 aynı oturumda tamamlanır.
- Rol şifresi user-secrets'a (quickstart ön koşul 2); appsettings'e YAZILMAZ.