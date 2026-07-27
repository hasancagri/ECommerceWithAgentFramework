# Tasks: IngestionAgent LLM-Sürücülü Yazıcılar

**Input**: Design documents from `specs/015-ingestion-llm-writers/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/writer-agents.md, quickstart.md

**Tests**: Anayasa kalite kapısı gereği dar kapsamlı birim testleri dahil (WriterResult sözleşmesi, kısa-devre); tam TDD değil.

**Organization**: Görevler user story bazında gruplu; FR-015 spike'ı foundational fazda ve her şeyi bloklar.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

**Purpose**: Paketler ve test projesi iskeleti.

- [X] T001 IngestionAgent.csproj'a Microsoft.Agents.AI + Microsoft.Extensions.AI(.OpenAI) referansları ekle (sürümler CPM'de mevcut, sürümsüz ekle)
- [X] T002 [P] tests/IngestionAgent.Tests projesi oluştur (xUnit+Shouldly, CPM), slnx'e ekle

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Spike + tüm story'lerin paylaştığı yapı taşları.

**⚠️ CRITICAL**: T003 spike'ı (FR-015) geçmeden workflow rewiring'e başlanmaz.

- [X] T003 SPIKE (FR-015): tests/IngestionAgent.Tests/WorkflowSemanticsSpikeTests.cs — conditional edge + terminal collector + tamamlanma; bulguyu research.md R3'e not düş
- [X] T004 src/agents/IngestionAgent/Program.cs — OpenAI:ApiKey+Model fail-fast (ikisi de zorunlu, default yok) + IChatClient singleton (FR-014, R7)
- [X] T005 [P] src/agents/IngestionAgent/Workflows/WriterResult.cs — WriterResult + CatalogWriterResult sözleşmesi (data-model.md kuralları)
- [X] T006 [P] src/agents/IngestionAgent/Infrastructure/AnonymousMcpTool.cs — AIFunction sarmalayıcı, çağrı başına taze MCP session (R4)
- [X] T007 src/agents/IngestionAgent/Infrastructure/McpToolCatalog.cs — lazy discovery + allowlist filtreleme → AITool listesi (T006'ya bağlı)

**Checkpoint**: Spike sonucu belli, yapı taşları hazır — story'ler başlayabilir.

---

## Phase 3: User Story 1 - Feed değişikliği LLM-sürücülü agent'la yansır (Priority: P1) 🎯 MVP

**Goal**: Üç yazma adımı da kendi ChatClientAgent'ıyla LLM üzerinden tool çağırır; feed → doğru katalog/stok/indirim durumu.

**Independent Test**: Feed değişikliği yayınla; log/trace'te LLM tool çağrılarını ve servis durumlarının snapshot'ı yansıttığını doğrula.

- [X] T008 [US1] src/agents/IngestionAgent/Workflows/01_CatalogWrite/CatalogWriterAgent.cs — catalog-writer ChatClientAgent: prompt + structured output + upsert_product allowlist
- [X] T009 [US1] src/agents/IngestionAgent/Workflows/01_CatalogWrite/CatalogWriteExecutor.cs — agent'ı koştur; CatalogWriterResult'tan ProductId/Failure'ı RecordJob'a yaz (FR-006)
- [X] T010 [P] [US1] Workflows/02_StockWrite/ (03'ten yeniden adlandır) — stock-writer agent + executor: set_stock, ProductId koddan gelir
- [X] T011 [P] [US1] Workflows/03_DiscountWrite/ (02'den yeniden adlandır) — discount-writer agent + executor: DiscountPercent boş→remove, dolu→set (FR-013)
- [X] T012 [US1] src/agents/IngestionAgent/Program.cs — üç yazıcı agent'ın Singleton DI kaydı; eski deterministik agent kayıtlarını sök
- [X] T013 [US1] src/agents/IngestionAgent/Workflows/SupplierSnapshotHandler.cs — workflow'u spike sonucuna göre terminal collector'lu şekle geçir; başarı yolu uçtan uca
- [X] T014 [US1] Canlı doğrulama: quickstart.md Senaryo 1 (feed → 3 LLM adımı → durum yansır; indirimsizde remove etkisiz-başarı)

**Checkpoint**: Başarılı akış LLM-sürücülü çalışıyor — MVP.

---

## Phase 4: User Story 2 - Hata/retry/DLQ garantileri korunur (Priority: P1)

**Goal**: Başarısız adım sonrası hiçbir adım (LLM dahil) koşmaz; mesaj kademeli retry sonrası kimlik+hata koduyla DLQ'ya düşer.

**Independent Test**: stock-api kapalıyken feed yayınla; discount adımının hiç koşmadığını, retry sonrası DLQ'da ExternalId+kod olduğunu doğrula.

- [X] T015 [US2] Workflows/SupplierSnapshotHandler.cs — conditional edge'lerle short-circuit: başarısız adım → doğrudan terminal; sonraki LLM hiç çağrılmaz (FR-003)
- [X] T016 [US2] Adım timeout'u: Ingestion:StepTimeoutSeconds (default 60) her agent koşumunu sarar; timeout=adım hatası; WORKFLOW_INCOMPLETE korunur (FR-005, R5)
- [X] T017 [P] [US2] tests/IngestionAgent.Tests/ — birim testleri: kısa-devre koşulu, WriterResult→Failure kod eşleme, incomplete guard
- [X] T018 [US2] Canlı doğrulama: quickstart.md Senaryo 2 (stock kapalı → catalog OK/stock FAIL/discount koşmaz → retry 10/30/60 → DLQ → replay yakınsar)

**Checkpoint**: Dış davranış (at-least-once, retry/DLQ, sessiz-ack=0) bugünküyle bire bir.

---

## Phase 5: User Story 3 - Zarf-parse sürtünmesi kalkar, yazıcılar tekdüzeleşir (Priority: P2)

**Goal**: Elle zarf parse makinesi tamamen silinir; üç yazıcı aynı WriterResult sözleşmesini paylaşır.

**Independent Test**: Zarf ayna tipleri grep'i 0 sonuç; üç executor da WriterResult kullanır; build+test yeşil.

- [X] T019 [US3] Infrastructure/McpConnector.cs + McpToolInvoker.cs sil (ToolData/ToolMessage/ToolResponse/ToolOutcome dahil); kalan referansları temizle (FR-012)
- [X] T020 [P] [US3] tests/IngestionAgent.Tests/ — WriterResult deserialization sözleşme testleri (geçerli/bozuk JSON/catalog'da eksik productId → hata)
- [X] T021 [US3] Doğrulama: `grep -rn "ToolOutcome\|ToolResponse\|ToolMessage\|ToolData" src/agents/IngestionAgent` → 0 (SC-005); dotnet build + dotnet test yeşil

**Checkpoint**: Tüm story'ler bağımsız doğrulanmış durumda.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T022 [P] CLAUDE.md ingestion bölümünü güncelle: "MCP tool'larını LLM'siz doğrudan çağırır" cümlesi → LLM-sürücülü yazıcılar (015)
- [X] T023 [P] Canlı doğrulama: quickstart.md Senaryo 4 — ApiKey/Model eksikken açılış fail-fast, geri ekleyince normal
- [X] T024 Obsidian vault'a gerekçe notu: 007 "NO LLM writers" duruşunun tersine çevrilmesi (manuel senkron kuralına uygun)

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (P1) → Foundational (P2) → US1 → US2 → US3 → Polish.
- T003 spike'ı T013/T015 workflow rewiring'ini bloklar; T007, T006'ya; T009, T008'e bağlıdır.
- US2, US1'in workflow'u üzerine kurulur (aynı dosya: SupplierSnapshotHandler) — sıralı ilerle.
- US3 silme işi (T019) ancak üç executor yeni yola geçince (US1 sonu) güvenlidir.

### Parallel Opportunities

- T001 ∥ T002; T005 ∥ T006 (T004 ile de paralel); T010 ∥ T011 (farklı klasörler); T017 ve T020 kod işleriyle paralel; T022 ∥ T023.

---

## Implementation Strategy

- **MVP = US1**: Setup + Foundational + Phase 3; Senaryo 1 canlı doğrulaması ile dur ve değerlendir.
- Sonra US2 (garanti eşitliği kanıtı), sonra US3 (temizlik) — her checkpoint'te bağımsız doğrula.
- Tek geliştirici varsayımı: sıralı akış; commit'ler görev veya mantıksal grup başına.