---
description: "Task list — Product Enrichment Agent"
---

# Tasks: Product Enrichment Agent

**Input**: `/specs/002-product-enrichment-agent/` (spec.md, plan.md, research.md,
data-model.md, contracts/mcp-tools.md)

**Tests**: Yalnız plan'ın istediği saf domain birim testleri dahil (Product davranış
metotları). Agent/Workflow ve File upload canlı quickstart ile doğrulanır.

**Organization**: Görevler user story'lere göre gruplanır; her story bağımsız test edilir.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

- **[P]**: Paralel çalışabilir (farklı dosya, tamamlanmamış bağımlılık yok).
- **[Story]**: US1/US2/US3 (Setup/Foundational/Polish etiketsiz).

---

## Phase 1: Setup (Ortak altyapı)

**Purpose**: Proje iskeleti + paket sürümleri; hiçbir story mantığı yok.

- [X] T001 [P] Directory.Packages.props: Microsoft.Agents.AI + Workflows (1.13.0) sürümleri; ImageSharp zaten vardı
- [X] T002 [P] Yeni proje src/agents/ProductEnrichmentAgent (WebApi + BackgroundService iskeleti) oluştur; ECommerceWithAgentFramework.slnx'e ekle
- [X] T003 AppHost.cs: enrichment-agent resource + gateway/Identity referansları (Aspire service discovery)

---

## Phase 2: Foundational (Bloklayan önkoşullar)

**Purpose**: Kimlik/yetki + File MCP yüzeyi + agent MCP/token altyapısı. Tüm story'ler buna bağlıdır.

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlayamaz.

- [X] T004 [P] Common Utils/Constants/AuthorizationScopes.cs: FileWrite scope sabiti ekle
- [X] T005 Config.cs: file.write ApiScope + file.api resource'una ekle; enrichment.agent client (m2m.client deseni: ClientCredentials+Secret), scope catalog.read/write+file.write
- [X] T006 File.Api Program.cs: app.MapMcp("/mcp") + UseStaticFiles(Images) + file.write scope + Wolverine ScopeAuthorizationMiddleware
- [X] T007 ProductEnrichmentAgent ClientCredentialsTokenHandler.cs: enrichment.agent CC token alır+önbellekler, MCP isteklerine Bearer ekler
- [X] T008 ProductEnrichmentAgent Program.cs: OpenAI chat+image client, Catalog+File MCP client (token handler + resilience-muaf), agent/workflow DI
- [X] T009 Gateway: file-images-route (/file/images statik) + file-mcp-route (/mcp/file) eklendi (SC-003)

**Checkpoint**: Yetki, File MCP ve agent token/MCP hattı hazır; user story'ler başlayabilir.

---

## Phase 3: User Story 1 - Eksik ürün AI ile tamamlanıp satışa çıkar (Priority: P1) 🎯 MVP

**Goal**: Tek bir eksik ürün için uçtan uca hat: açıklama + gerçek görsel üretilir, Catalog'a
yazılır, ürün tam olur ve (aktifse) satışta görünür.

**Independent Test**: Açıklama/görsel boş tek aktif ürün alınır; agent o ürün için tetiklenir;
sonrasında açıklama+görsel dolu ve müşteri aramasında satışta görünür.

### Domain testleri (US1) ⚠️

> Önce yaz, kırmızı gör, sonra T012'yi uygula.

- [X] T010 [P] [US1] Domain test tests/Catalog.Api.Tests: SetDescriptionIfEmpty/SetImageUrlIfEmpty — boşsa yazar+recalculate, doluysa dokunmaz (8 test)

### Implementation (US1)

- [X] T011 [P] [US1] (İptal) Written/Skipped ayrımı kaldırıldı — "boşsa doldur, doluysa dokunma" yeterli; resource kodu/enum gerekmez
- [X] T012 [US1] Catalog Domains/Products/Product.cs: SetDescriptionIfEmpty + SetImageUrlIfEmpty:void (boşsa yaz+RecalculateCompleteness, doluysa dokunma)
- [X] T013 [US1] Catalog Features/Agent/ListIncompleteProducts.cs: query IsComplete==false && IsDeleted==false → {Id,Name,Brand,HasDescription,HasImage}
- [X] T014 [US1] Catalog Features/Commands/SetProductDescription.cs [Transactional][RequiredScope(catalog.write)]: SetDescriptionIfEmpty → {Id}; ürün yok→NotFound
- [X] T015 [US1] Catalog Features/Commands/SetProductImage.cs [Transactional][RequiredScope(catalog.write)]: product.SetImageUrlIfEmpty → {Id}
- [X] T016 [US1] Catalog ProductMcpTools.cs: list_incomplete_products, set_product_description, set_product_image ince tool sarmalayıcıları (IMessageBus)
- [X] T017 [US1] File Domains/Images/Features/Commands/UploadImage.cs [RequiredScope(file.write)]: byte'ları 256×256 resize edip Images/{ProductId}.png'e yaz, URL döner
- [X] T018 [US1] File Domains/Images/ImageMcpTools.cs: upload_product_image ince tool sarmalayıcısı ekle
- [X] T019 [P] [US1] Agent Agents/DescriptionAgent.cs: ChatClientAgent ile ad+marka → ≤100 karakter açıklama (prompt garanti + sert kes)
- [X] T020 [P] [US1] Agent Agents/ImageAgent.cs: ChatClientAgent image prompt kurar, OpenAI gpt-image-1 (LowQuality, 1024²) PNG bytes üretir
- [X] T021 [US1] Agent EnrichmentWorkflow.cs: Agent Framework Workflows executor-graph — DescriptionAgentExecutor→ImageAgentExecutor, InProcessExecution ile koşulur (bkz. Notes)
- [X] T022 [US1] Agent EnrichmentBackgroundService.cs: eksik ürünleri çekip workflow'u çalıştırır (tek ürün de bu döngüyle işlenir)

**Checkpoint**: US1 tek başına çalışır — bir ürün uçtan uca tamamlanıp satışa çıkar.

---

## Phase 4: User Story 2 - Eksik envanterin toplu zenginleştirilmesi (Priority: P2)

**Goal**: Tüm eksik ürünler (30 seed) toplu işlenir; ürün başına başarı/başarısızlık raporlanır;
tek hata koşuyu durdurmaz.

**Independent Test**: Çok eksik ürünle katalog doldurulur; toplu koşu çalışır; büyük çoğunluk
tam+satışta olur, sonuç ürün başına durum içerir.

### Implementation (US2)

- [X] T023 [US2] Agent Dtos.cs: EnrichmentResult (ProductId + Description/Image: FieldResult Ok/Skipped/Failed)
- [X] T024 [US2] EnrichmentBackgroundService.cs: list_incomplete_products ile tüm eksikleri çek, sırayla (az-eşzamanlı) işle
- [X] T025 [US2] EnrichmentBackgroundService: her ürün try/catch — tek hata koşuyu durdurmaz (FR-007)
- [X] T026 [US2] Agent LogReport: ürün başına başarı/atlandı/hata + özet sayaç loglanır

**Checkpoint**: US1 + US2 bağımsız çalışır — toplu koşu kataloğu satışa hazırlar.

---

## Phase 5: User Story 3 - Güvenli ve tekrar-edilebilir zenginleştirme (Priority: P3)

**Goal**: Dolu alan üzerine yazılmaz, tam ürün atlanır, başarısızlık ürünü önceki durumda bırakır,
tekrar koşu ek üretim/masraf üretmez (idempotent).

**Independent Test**: Kısmen tam ürünlerle iki kez koş; tam ürünler dokunulmaz, ikinci koşu yeni
değişiklik/üretim üretmez.

### Domain testleri (US3) ⚠️

- [X] T027 [P] [US3] Domain test (ProductEnrichmentTests): RepeatedEnrichment_OnAlreadyCompleteProduct_LeavesUnchanged + IfEmpty koruma (üzerine-yazma %0)

### Implementation (US3)

- [X] T028 [P] [US3] File UploadImage: Images/{ProductId}.png varsa yeniden üretmeden mevcut URL döner (dosya varlık kontrolü — FR-010) — T017'de yapıldı
- [X] T029 [P] [US3] Agent EnrichmentMcpClient.CallAsync: geçici MCP hatalarında 3-deneme backoff retry; tükenirse alan eksik kalır (FR-011)
- [X] T030 [US3] Skipped rapora yansır: workflow HasDescription/HasImage ön-kontrolüyle dolu alanı FieldResult.Skipped işaretler (SC-004; T011 iptaliyle uyumlu)

**Checkpoint**: Üç story de bağımsız çalışır; tekrar koşu güvenli ve idempotent.

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Uçtan uca doğrulama ve kalite.

- [X] T031 quickstart.md doğrulaması: canlı Aspire'da 30 seed ürünü enrich edildi, satışa-hazır 0→30 (SC-002) — DOĞRULANDI 2026-07-13
- [X] T032 [P] Description/Image promptları canlı doğrulandı: gerçek Türkçe açıklamalar + gerçek PNG'ler, placeholder yok (SC-003) — 2026-07-13
- [X] T033 dotnet build + dotnet test tüm çözüm yeşil (67 test); agent-özel sabitler ConstValues'ta, GlobalUsings düzenli
- [X] T034 Agent ImageAgent.cs: OpenAI 429 (image hız-limiti) için Retry-After'a uyan üstel backoff; tükenirse alan eksik kalır (FR-011) — canlı doğrulandı
- [X] T035 WebApp Program.cs: /file/images/{name} anonim proxy → file-api (iç servis); tarayıcı görselleri aynı origin'den görür (SC-003 görüntüleme)

---

## Dependencies & Execution Order

### Phase bağımlılıkları

- **Setup (P1)**: bağımsız başlar.
- **Foundational (P2)**: Setup'a bağlı; TÜM user story'leri bloklar.
- **User Stories (P3+)**: Foundational sonrası; öncelik sırası US1 → US2 → US3.
- **Polish (P6)**: istenen story'ler bitince.

### User Story bağımlılıkları

- **US1 (P1)**: Foundational sonrası başlar; başka story'ye bağlı değil (MVP).
- **US2 (P2)**: US1 hattının üstüne kurulur (aynı workflow/service); bağımsız test edilebilir.
- **US3 (P3)**: US1 aggregate metotları + File upload üzerine güvenlik/idempotency ekler.

### Story içi sıra

- Testler (varsa) önce yazılır ve kırmızı görülür.
- Resource sabiti → aggregate metodu → command → MCP tool → agent/workflow.
- Aggregate metotları (T012) command'lardan (T014/T015) önce.

### Paralel fırsatlar

- Setup T001-T002 [P]; T003 (AppHost) sıralı.
- US1: T010 (test) ve T011 (resource) [P]; T019/T020 (iki agent, farklı dosya) [P].
- US3: T027/T028/T029 [P] (farklı dosya/BC).
- US1 tamamlanınca US2 ve US3 farklı geliştiricilerce paralel yürütülebilir.

---

## Parallel Example: User Story 1

```bash
# Agent'ları birlikte başlat (farklı dosya):
Task: "Agents/DescriptionAgent.cs — ad+marka → açıklama"
Task: "Agents/ImageAgent.cs — image prompt + gpt-image-1 bytes"
```

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1.
4. **DUR & DOĞRULA**: tek ürünü uçtan uca tamamla, satışta gör.

### Incremental Delivery

1. Setup + Foundational → hat hazır.
2. US1 (T001–T022) → tek ürün doğrula → MVP.
3. US2 → toplu koşu + rapor doğrula.
4. US3 → idempotency/güvenlik doğrula.

---

## Notes

- [P] = farklı dosya, bağımlılık yok.
- Agent stateless; dayanıklılık/idempotency çağrılan servislerin MCP handler'larında (plan).
- Bounded context sınırı sert: agent yalnız MCP ile yazar, hiçbir DB'ye dokunmaz.
- Kısmi başarı: bir alan başarısızsa ürün eksik kalır (IsComplete false → satışa çıkmaz).
- **T021 (Workflows) — TAMAMLANDI 2026-07-13**: `EnrichmentWorkflow` gerçek
  `Microsoft.Agents.AI.Workflows` executor-graph'ine taşındı: `DescriptionAgentExecutor`
  (IncompleteProduct→EnrichmentState) → `ImageAgentExecutor` (EnrichmentState→EnrichmentResult),
  `WorkflowBuilder.AddEdge<T>/WithOutputFrom`, ürün başına `InProcessExecution.RunAsync`.
  Canlı doğrulandı: 30/30 ürün enrich (açıklama+görsel), IsComplete.
- **API gotcha (1.13.0)**: `ReflectingExecutor`/`IMessageHandler` obsolete + source-gen yok;
  `Executor`+`ConfigureProtocol`(`SendsMessage`/`YieldsOutput`/`AddHandler`) deseni kullanıldı.
- **429 (T034)**: image hız-limiti File.Api'den değil OpenAI'den; backoff ImageAgent'ta.
- **Görsel görüntüleme (T035)**: File.Api iç servis; URL gateway-göreli /file/images/{id}.png;
  tarayıcı WebApp aynı-origin proxy'siyle erişir (browser 'http://file-api'yi çözemez).