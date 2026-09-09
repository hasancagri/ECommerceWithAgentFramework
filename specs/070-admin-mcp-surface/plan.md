# Implementation Plan: Admin Yüzeyinin MCP'ye Taşınması (Agent-Only Yönetim)

**Branch**: `070-admin-mcp-surface` | **Date**: 2026-09-09 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/070-admin-mcp-surface/spec.md`

## Summary

Son ekran-bağımlı yüzey olan admin işlemleri (ürün künye/yayın/fiyat-geçmişi, stok, merchant kimlik,
onboarding) MCP tool'larına taşınır; dış agent'lar (Claude Desktop) için seed'li yönetim OAuth
istemcisi açılır; müşteri paritesindeki son delik (taksit sorgusu) ve keşif rehberi (069 playbook)
sunucu tarafına iner. Teknik yaklaşım: her BC'de İKİNCİ korumalı `/mcp-admin` ucu + mevcut admin REST
dilimlerinin `Features/Agents` ikizleri + AgentQueryLog emsali salt-append denetim izi. WebApp/ChatAgent
sökümü KAPSAM DIŞI (071 adayı).

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings)

**Primary Dependencies**: Marten (Postgres), Wolverine (IMessageBus), OpenIddict + ASP.NET Identity,
ModelContextProtocol (MCP server/client), A2A istemci paketi (Order.Api'ye YENİ referans; sürüm
Directory.Packages.props'ta mevcut), YARP (gateway rotaları), Scrutor

**Storage**: BC başına Postgres/Marten — yeni doküman yalnız `AdminActionLog` (catalogDb/stockDb/
customerDb, salt-append); şema `ApplyAllDatabaseChangesOnStartup` ile otomatik

**Testing**: xUnit + Shouldly (saf domain — bu feature'da yeni domain davranışı yok/minimal);
asıl doğrulama quickstart.md canlı senaryoları + guard script'leri

**Target Platform**: Aspire AppHost altında servisler (darwin dev; container'lı Postgres/RabbitMQ/Mailpit)

**Project Type**: Mikroservis (DDD/VSA); değişen servisler: Catalog.Api, Stock.Api, Customer.Api,
Order.Api, Storefront.Api, Identity.Server, Gateway, Shared

**Performance Goals**: Belirgin hedef yok — admin/keşif etkileşimli kullanım; mevcut sorgu tavanları
(50 satır) korunur

**Constraints**: PaymentGateway repo'suna DOKUNULMAZ; anonim keşif MCP uçları bozulmaz; DCR scope
tavanı (`ExternalAgentDefaults`) değişmez; ChatAgent bu feature boyunca ÇALIŞIR kalır (FR-013);
merchant key hiçbir yanıt/izde düz metin dönmez

**Scale/Scope**: ~10 yeni MCP tool + 3 yeni `/mcp-admin` ucu + 1 seed OAuth istemcisi + playbook
göçü + guard retarget; tek admin kullanıcı, düşük hacim

## Constitution Check

*GATE: Phase 0 öncesi değerlendirildi; Phase 1 tasarımı sonrası yeniden kontrol edildi — SONUÇ AŞAĞIDA.*

| İlke | Durum | Not |
|---|---|---|
| I — BC izolasyonu | ✅ | Tool'lar sahibi BC'de; DB paylaşımı yok; S2S mevcut sözleşmeler (basket gRPC, customer internal REST). İKİ sapma gerekçeli → Complexity Tracking |
| I — MCP'yi yalnız agent tüketir | ⚠️ SAPMA | Onboarding sarmalayıcı: Customer.Api, PG Merchant.Api MCP'sini imperatif çağırır — dış solution YALNIZ MCP sunuyor + PG dokunulmaz. Tek slice'a hapsedildi (R6) |
| II — Zengin aggregate | ✅ | Yeni aggregate/davranış yok; slice'lar mevcut metotları çağırır. AdminActionLog aggregate değil (iz dokümanı; muafiyet emsali AgentQueryLog) |
| III — VSA + CQRS | ✅ | Her tool = `Features/Agents/<X>ForAgent` izole slice (Commands/Queries reuse YOK — bilinçli tekrar); repository yok |
| IV — Result pattern | ✅ | Handler'lar Feature*ResultModel; hata kodları `<Service>ResourceConstants` |
| V — Scope yetkisi | ✅ | Endpoint `RequireAuthorization` + handler `[RequiredScope]`; scope'lar mevcut kapalı registry'den (yeni scope YOK); rol=scope demeti korunur |
| VI — Domain-TDD | ✅ | Yeni saf domain davranışı beklenmiyor; çıkarsa (ör. stok adjust guard'ı eksikse) test-first. Handler/endpoint test-sonra/canlı |
| VII — FLOW.md | ✅ | Domain SÜRECİ değişmiyor (yüzey değişiyor); FLOW güncellemesi gerekmez. Onboarding sarmalayıcı Customer sürecine adım eklemiyor (dış çağrı vekili) |

**A2A notu (sapma DEĞİL):** Order.Api'nin PG A2A skill'ini çağırması MCP yasağına girmez (yasak
spesifik olarak MCP; anayasa v1.8.1). Yine de teknik borç olarak işaretli (R5): PG REST sunarsa indirilir.

## Project Structure

### Documentation (this feature)

```text
specs/070-admin-mcp-surface/
├── plan.md              # bu dosya
├── research.md          # Phase 0 — 9 karar (R1-R9)
├── data-model.md        # Phase 1 — AdminActionLog + seed istemci + quote şekli
├── quickstart.md        # Phase 1 — canlı doğrulama senaryoları
├── contracts/
│   ├── admin-mcp-tools.md       # 3 BC'nin /mcp-admin tool sözleşmeleri
│   ├── customer-agent-tools.md  # quote_installments
│   └── seeded-admin-client.md   # external-admin-agent OAuth istemcisi
└── tasks.md             # /speckit-tasks üretecek (bu komut DEĞİL)
```

### Source Code (repository root)

```text
src/others/Shared/McpToolNames.cs                 # + CatalogAdminTools, StockAdminTools,
                                                  #   CustomerAdminTools, OrderTools.QuoteInstallments
src/others/Identity.Server/Config.cs              # + external-admin-agent seed istemcisi
src/services/catalog/Catalog.Api/
├── Domains/Products/Features/Agents/             # + AdminListProductsForAgent, AdminGetProductForAgent,
│                                                 #   AdminUpdateProductForAgent, AdminSetPublishedForAgent,
│                                                 #   AdminGetPriceHistoryForAgent
├── Domains/Products/ProductMcpTools.cs           # + admin tool sarmalayıcıları (ayrı ToolType sınıfı)
├── AdminAudit/AdminActionLog.cs                  # + iz dokümanı (Domains dışı; aggregate değil)
└── Program.cs                                    # + MapMcp("/mcp-admin").RequireAuthorization() + PRM
src/services/stock/Stock.Api/                     # aynı desen: 2 agent slice + AdminActionLog + /mcp-admin
src/services/customer/Customer.Api/
├── Domains/MerchantInformations/Features/Agents/ # + AdminGetMerchantStatusForAgent,
│                                                 #   AdminSetMerchantCredentialsForAgent,
│                                                 #   AdminSubmitOnboardingForAgent, AdminOnboardingStatusForAgent
├── Onboarding/                                   # + PG Merchant.Api MCP istemcisi + makine-token handler
│                                                 #   (ChatAgent OnboardingGatewayTokenHandler deseni taşınır)
├── AdminAudit/AdminActionLog.cs
└── Program.cs                                    # + /mcp-admin + PRM
src/services/order/Order.Api/
├── Domains/Orders/Features/Agents/QuoteInstallmentsForAgent.cs   # basket gRPC + payment-context S2S + A2A quote
├── A2A/PaymentAgentQuoteClient.cs                # A2A istemci sarması (Http named client, resilience-muaf)
└── Domains/Orders/OrderMcpTools.cs               # + quote_installments sarmalayıcı
src/services/storefront/Storefront.Api/Domains/StorefrontView/StorefrontMcpTools.cs
                                                  # Description'a playbook göçü (sadeleştirilmiş)
src/services/gateway/Gateway/                     # + /mcp-admin/* ve PRM rotaları
scripts/check-agent-query-schema.sh               # prompt_file → StorefrontMcpTools.cs
```

**Structure Decision**: Mevcut VSA yerleşimi korunur; her tool sahibi BC'nin `Features/Agents`
dilimi + `*McpTools` sarmalayıcısı. Yeni servis/proje AÇILMAZ. AdminActionLog `Domains/` DIŞINDA
(aggregate değil; AgentSql/AgentQueryLog emsal yerleşimi).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Agent-olmayan kod (Customer.Api) PG Merchant.Api MCP'sini imperatif çağırır (İlke I / v1.8.1) | Onboarding submit/status dış solution'da YALNIZ MCP olarak var; PG repo'suna dokunmak yasak (kullanıcı kısıtı) | (a) PG'ye REST eklemek → kısıt ihlali; (b) admin'in Claude Desktop'ını PG MCP'sine doğrudan bağlamak → kullanıcı kararıyla RED (tek bağlantı + bizim iz/scope istendi, FR-016); (c) özelliği düşürmek → Docker-reset kurtarma yolu kopar |
| Geçici çift playbook kopyası (tool description ↔ ChatAgent prompt) | FR-013: ChatAgent bu feature boyunca bozulmamalı; kanonik ev tool description'a geçiyor | Tek kopyaya İNDİRİLEMEZ: ChatAgent prompt'u compile-time const ve Storefront.Api'ye referans veremez; kopya söküm feature'ında (071) ölecek — guard yalnız yeni evi denetler |
