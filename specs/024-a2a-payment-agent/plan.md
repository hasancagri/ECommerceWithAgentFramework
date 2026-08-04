# Implementation Plan: ChatAgent A2A Installment Quote

**Branch**: `024-a2a-payment-agent` | **Date**: 2026-08-04 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/024-a2a-payment-agent/spec.md`

## Summary

ChatAgent assistant'ı, ayrı solution'daki uzak **A2A PaymentAgent**'a A2A istemci olarak
bağlar; kullanıcı "default kartımla sepet tutarı için taksitler" dediğinde assistant sepet
toplamını (Basket MCP `get_basket`) + default kartın **BIN**'ini (Customer BC) alır, uzak
agent'ın `installment_quote` skill'ine NL sorgu delege eder ve o bankaya özel taksit tablosunu
listeler. Read-only; PAN/CVV/token yok. Uzak taraf yoksa graceful-degrade. Küçük Customer BC
eki: `SavedCard.Bin` yakalama. Auth (merchant key) şimdilik ertelendi.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: MAF `Microsoft.Agents.AI` 1.13.0 + **`Microsoft.Agents.AI.A2A`
(prerelease 1.13.0-preview.\*)** + **`A2A` (1.0.0-preview2)** [istemci]; `Microsoft.Extensions.AI`
(OpenAI); `ModelContextProtocol.Core` (mevcut). Server A2A paketleri KULLANILMAZ (uzak solution).

**Storage**: Marten (Customer BC `customerDb`) — `SavedCard`'a `Bin` alanı; şema migration'sız
(document, eski kayıtta null → BIN'siz fallback). ChatAgent state'siz (taksit verisi kalıcı değil).

**Testing**: xUnit + Shouldly; saf domain birim testi — `SavedCard` BIN yakalama/doğrulama.
A2A istemci host gerektirir → quickstart senaryolarıyla canlı doğrulama.

**Target Platform**: Linux/container; Aspire AppHost orkestrasyonu (chat-agent resource).

**Project Type**: Dağıtık mikroservis + AI agent (mevcut). Değişen: `src/agents/ChatAgent`
(A2A istemci) + `src/services/customer` (BIN). Uzak sunucu tarafı bu repoda DEĞİL.

**Performance Goals**: Etkileşimli sohbet; A2A çağrısı SSE, kullanıcı-algısı saniyeler. Özel SLA yok.

**Constraints**: PAN/CVV/token asla A2A/LLM kanalında (SERT, PCI). A2A HttpClient resilience-muaf
(SSE). Graceful-degrade zorunlu (uzak taraf yok). Agent tipleri Singleton (İlke).

**Scale/Scope**: Tek yeni tool (uzak agent sarmalayıcı) + 1 küçük Customer BC alanı + prompt
intent maddesi + default-kart-BIN okuma yüzeyi. Yeni aggregate/servis/event YOK.

## Constitution Check

*GATE: Phase 0 öncesi ve Phase 1 sonrası.*

- **İlke I (BC İzolasyonu):** ✅ A2A = context-lar-arası **bilinçli dış kanal** (MCP/gRPC gibi);
  uzak agent'ın DB/tablosuna erişim yok, kontrat (AgentCard + skill) üzerinden tüketim. ChatAgent
  Customer/Basket'e mevcut MCP ile erişir, DB'ye değil. BIN Customer BC içinde kalır.
- **İlke II (Zengin Aggregate):** ✅ `SavedCard` sade entity (Wallet aggregate içinde); BIN
  yakalama davranışı aggregate/entity'de, handler'da iş kuralı yok. Yeni anemik aggregate yok.
- **İlke III (Vertical Slice + CQRS):** ✅ BIN okuma = Query/Agent slice; MCP tool ince sarmalayıcı.
  ChatAgent feature-slice'a tabi değil (agent host) ama mevcut desene uyar.
- **İlke IV (Result Pattern):** ✅ Customer BC değişiklikleri Result döner; ChatAgent tarafı
  agent framework (Result kapsamı dışı, host katmanı).
- **İlke V (Scope-auth, rol yok):** ✅ Rol getirilmez. Merchant-key ertelendi; eklenince
  scope-tabanlı. Mevcut kullanıcı token akışı (MCP) değişmez.

**Sonuç:** İhlal yok. A2A kanalı İlke I'in "bilinçli sözleşmeli dış iletişim" kapsamında;
mevcut amendment'lar (gRPC v1.2.0) ruhuyla tutarlı — anayasa değişikliği GEREKMEZ (dış istemci,
servisler-arası senkron RPC değil). Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/024-a2a-payment-agent/
├── plan.md              # bu dosya
├── research.md          # Phase 0 — A2A API kararları (doğrulanmış)
├── data-model.md        # Phase 1 — SavedCard.Bin + geçici görüntü verisi
├── quickstart.md        # Phase 1 — doğrulama senaryoları
├── contracts/
│   └── a2a-installment-agent.md   # A2A AgentCard + installment_quote I/O kontratı
├── checklists/
│   └── requirements.md  # spec kalite checklist (✓)
└── tasks.md             # /speckit-tasks çıktısı (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/agents/ChatAgent/                      # A2A İSTEMCİ (ana değişiklik)
├── Program.cs                             # A2A url oku; card-resolve; AsAIFunction; assistant tools'a ekle
├── A2AInstallmentTool.cs (yeni)           # uzak agent'ı sarmalayan kurulum + graceful-degrade
├── MerchantKeyHandler.cs (yeni, İLERİDE)  # auth ertelendi — genişleme noktası (şimdilik yok)
├── ConstValues.cs                         # A2A skill/agent isim sabitleri (installment_quote)
└── Prompts / AssistantInstructions        # taksit-intent + ödeme-red maddeleri

src/services/customer/Customer.Api/Domains/Wallets/   # BIN (küçük ek)
├── SavedCard.cs                           # +Bin alanı, Create imzası, doğrulama
├── Tokenization/ICardTokenizer.cs         # TokenizeResult +Bin
├── Tokenization/SimulatedCardTokenizer.cs # ilk 6 haneyi Bin döndür
├── Features/Commands/AddCard.cs           # Bin'i SavedCard'a taşı
└── Features/Agent/GetCards.cs (veya yeni GetDefaultCardBin) + WalletMcpTools  # default BIN okuma

Directory.Packages.props                   # +Microsoft.Agents.AI.A2A (prerelease) +A2A 1.0.0-preview2
src/aspire/AppHost/AppHost.cs              # chat-agent'a A2A url config (opsiyonel/boş olabilir)
```

**Structure Decision**: Mevcut dağıtık yapı korunur. İki dokunuş noktası: (1) ChatAgent'a A2A
istemci hattı (asıl feature), (2) Customer BC'ye BIN (girdi önkoşulu). Uzak A2A sunucusu ayrı
solution'da; bu repoda yalnız istemci + kontrat dokümanı.

## Complexity Tracking

Anayasa ihlali yok — boş.