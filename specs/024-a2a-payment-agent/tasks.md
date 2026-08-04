# Tasks: ChatAgent A2A Installment Quote

**Input**: `/specs/024-a2a-payment-agent/` (spec.md, plan.md, research.md, data-model.md, contracts/)

**Tests**: Customer BC domain değişiyor (`SavedCard.Bin`) → saf birim testi VAR (constitution).
ChatAgent A2A = host katmanı → saf domain testi yok; quickstart ile canlı doğrulanır.

**Organization**: Görevler user story'ye göre. US1=BIN'li taksit (mutlu yol), US2=uzak agent
yokken graceful-degrade.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

- **[P]**: Paralel (farklı dosya, bağımlılık yok)
- **[Story]**: US1=taksit sorgulama, US2=graceful-degrade

## Path Conventions

- İstemci: `src/agents/ChatAgent/` (Program.cs, ConstValues.cs, yeni A2A dosyası)
- BIN: `src/services/customer/Customer.Api/Domains/Wallets/`
- Paketler: `Directory.Packages.props` (CPM); versionsuz ref `.csproj`'ta

---

## Phase 1: Setup

**Purpose**: A2A istemci paketleri + config anahtarı.

- [X] T001 `Directory.Packages.props`: `PackageVersion` ekle — `Microsoft.Agents.AI.A2A`
  `1.13.0-preview.*` (prerelease) + `A2A` `1.0.0-preview2` (açıkça pinle)
- [X] T002 `src/agents/ChatAgent/ChatAgent.csproj`: versionsuz `PackageReference` ekle —
  `Microsoft.Agents.AI.A2A` + `A2A`
- [X] T003 [P] `src/agents/ChatAgent/appsettings.json` + `appsettings.Development.json`:
  `PaymentGateway:A2AUrl` anahtarı (boş/eksik olabilir → graceful-degrade)
- [X] T004 [P] `src/aspire/AppHost/AppHost.cs`: chat-agent resource'una `PaymentGateway:A2AUrl`
  env/param geçir (opsiyonel; uzak taraf yoksa boş)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: A2A istemci altyapısı — named HttpClient + tool kurulum helper'ı. **US1 ve US2'den
önce biter.**

**⚠️ CRITICAL**: Bitmeden US başlayamaz.

- [X] T005 `src/agents/ChatAgent/ConstValues.cs`: A2A sabitleri — uzak agent adı
  `payment-gateway-agent`, skill id `installment_quote`, A2A named-client adı, config anahtarı
- [X] T006 `src/agents/ChatAgent/Program.cs`: A2A named HttpClient kaydı —
  `RemoveAllResilienceHandlers()` + cömert timeout (MCP client deseni, satır 68-74); **auth
  handler YOK** (merchant key ertelendi, FR-008)
- [X] T007 `src/agents/ChatAgent/A2AInstallmentTool.cs` (yeni): `A2AUrl` boşsa `null` döndür
  (tool yok); doluysa `A2ACardResolver(url, httpClient)` → `GetAgentCardAsync()` → `card.Skills`
  içinde `installment_quote` doğrula → `GetAIAgentAsync()` → `AsAIFunction()`; tüm yol
  try/catch **fail-open** (hata → null + log), boot çökmez (US2/FR-006)

---

## Phase 3: User Story 1 — BIN'li taksit sorgulama (P1) 🎯 MVP

**Goal**: Giriş yapmış kullanıcı "default kartımla sepet tutarı için taksitler" der; assistant
sepet toplamı + default kart BIN'ini uzak agent'a delege eder, banka-özel taksitleri listeler.

**Independent Test**: Sepette ürün + default kart olan kullanıcı taksit sorar → banka/taksit/
komisyon tablosu toplamla tutarlı listelenir (SC-001).

### Customer BC — BIN yakalama

- [X] T008 [P] [US1] `.../Wallets/SavedCard.cs`: `Bin` alanı (private set, expose) + `Create`
  imzasına `bin` param + doğrulama (6 haneli rakam; yoksa boş kabul)
- [X] T009 [P] [US1] `.../Wallets/Tokenization/ICardTokenizer.cs`: `TokenizeResult`'a `Bin` alanı
- [X] T010 [US1] `.../Wallets/Tokenization/SimulatedCardTokenizer.cs`: PAN'ın ilk 6 hanesini
  `Bin` olarak döndür (satır 13 `digits` mevcut); PAN/CVV yine yazılmaz
- [X] T011 [US1] `.../Wallets/Features/Commands/AddCard.cs`: `TokenizeResult.Bin`'i
  `SavedCard.Create(...)`'e taşı
- [X] T012 [US1] `.../Wallets/Features/Agent/GetCards.cs` (veya yeni `GetDefaultCardBin.cs`):
  default kartın `Bin` (+brand/last4) okuması; PAN/token expose ETME
- [X] T013 [US1] `.../Wallets/WalletMcpTools.cs`: default-kart-BIN okuma için MCP tool ince
  sarmalayıcı (aynı query'yi `IMessageBus` ile çağırır)
- [X] T014 [P] [US1] `tests/Customer.Api.Tests/` (yoksa oluştur): `SavedCard` BIN yakalama +
  6-hane doğrulama + BIN'siz karta graceful birim testleri (xUnit + Shouldly)

### ChatAgent — delege hattı

- [X] T015 [US1] `src/agents/ChatAgent/Program.cs`: assistant'a Customer BC default-BIN MCP
  tool'unu ekle (assistantAgentTools listesi) + T007 A2A tool'unu (null değilse) assistant
  `tools`'a ekle
- [X] T016 [US1] `src/agents/ChatAgent/ConstValues.cs` `Prompts.AssistantInstructions` (satır 76):
  taksit-intent maddesi — "taksit iste → `get_basket` toplam + default kart BIN oku → installment
  tool"; sepet boşsa çağırma (FR-004); **sepet toplamı alınamazsa (Basket hatası) çağırma, durumu
  bildir** (Edge Case); kısmi/biçimsiz yanıtta eksiği bildir, **alan uydurma** (FR-003, Edge Cases)

---

## Phase 4: User Story 2 — Uzak agent yokken güvenli çalışma (P2)

**Goal**: Uzak agent yapılandırılmamış/erişilemezken assistant çöker olmadan açılır; taksit-dışı
tüm yetenekler çalışır; taksit niyeti nazik "şu an kullanılamıyor" ile karşılanır.

**Independent Test**: A2AUrl boş/erişilemezken assistant başlar; arama+sepet+sipariş çalışır;
taksit niyeti düzgün degrade (SC-002).

- [X] T017 [US2] `src/agents/ChatAgent/Program.cs` + `A2AInstallmentTool.cs`: A2AUrl boş/resolve
  başarısız → tool eklenmez, assistant diğer tool'larla açılır (T007 null yolunu doğrula/bağla)
- [X] T018 [US2] `src/agents/ChatAgent/ConstValues.cs` `Prompts.AssistantInstructions`: taksit
  tool'u yok/çağrı hata verdiğinde nazik "taksit bilgisi şu an alınamıyor" talimatı; teknik
  hata/exception kullanıcıya sızmaz (FR-006)
- [X] T019 [US2] `src/agents/ChatAgent/ConstValues.cs` `Prompts.AssistantInstructions`: ödeme/
  charge niyeti ("öde/satın al") kapsam-dışı nazik red; uzak agent'a charge gönderilmez (FR-005, SC-003)

---

## Phase 5: Polish & Cross-Cutting

- [X] T020 [P] `Directory.Packages.props` + build: `dotnet build` PASS (prerelease restore) +
  `dotnet test` PASS (Customer BIN testleri)
- [X] T021 [P] Güvenlik doğrulaması (quickstart): A2A HTTP gövdesinde PAN/CVV/token YOK — yalnız
  amount+currency+bin; OpenAI/LLM context'inde de yalnız BIN+tutar
- [X] T022 Quickstart senaryoları canlı doğrula: S1 (BIN'li mutlu yol), S2 (boş sepet), S3
  (uzak kapalı degrade), S4 (default kart yok), S5 (ödeme reddi) — `quickstart.md`
- [X] T023 [P] Feature kapanışında memory (`a2a-payment-agent-direction`) + Obsidian
  (`todo-payment-gateway-*`) durum güncelle (MERGED + canlı sonuç)

---

## Dependencies

- **Phase 1 → Phase 2 → (Phase 3, Phase 4) → Phase 5.**
- US1 (Phase 3) = MVP; US2 (Phase 4) foundational fail-open (T007) üstüne biner, kısmen paralel
  başlanabilir ama US1 wiring (T015) sonrası doğrulanır.
- Customer BC (T008-T014) ChatAgent wiring'inden (T015-T016) bağımsız ilerler (farklı servis);
  T012/T013 MCP tool'u T015'in default-BIN okumasını sağlar → T015 onlara bağlı.

## Parallel Opportunities

- Setup: T003 ∥ T004.
- US1 domain: T008 ∥ T009 ∥ T014 (farklı dosya). T010→T011 sıralı (tokenizer→AddCard).
- Customer BC şeridi (T008-T014) ∥ ChatAgent altyapısı (Phase 2), farklı projeler.
- Polish: T020 ∥ T021 ∥ T023.

## Implementation Strategy

- **MVP = US1 (Phase 1+2+3).** Uzak A2A stub/mock ile T022-S1 doğrulanır (contract-first).
- US2 dayanıklılık dilimi; T007 fail-open ile büyük ölçüde bedava gelir, Phase 4 talimat +
  doğrulama ekler.
- Uzak `PaymentGateway` A2A sunucusu AYRI solution'da; bu tasks yalnız istemci + BIN + kontrat.