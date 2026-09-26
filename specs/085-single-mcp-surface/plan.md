# Implementation Plan: Tek MCP Yüzeyi — /mcp-admin Sökümü + Scope-Bazlı Tool Budaması

**Branch**: `085-single-mcp-surface` | **Date**: 2026-09-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/085-single-mcp-surface/spec.md`

## Summary

`/mcp-admin` ikinci yüzeyi ölür: fasat + 4 BC (catalog/stock/customer/discount) tüm tool'larını tek `/mcp`'den sunar. "Hangi tool görünür" sinyali yol-prefix'ten TOKEN SCOPE'a taşınır: BC `ConfigureSessionOptions` oturumu açan token'ın scope'larına bakar (scope başına budama); fasat listeyi scope-parmakizi anahtarlı cache'le kullanıcı token'ıyla toplar. IdP tarafında istemci-tavanı zorlaması OpenIddict ön-validasyonundan `ScopeResolver` kesişimine taşınır (union PRM'in müşteri istemcisini kırmaması için). RBAC omurgası, `[RequiredScope]` son savunması ve DCR tavanı değişmez.

## Technical Context

**Language/Version**: .NET 10 / C# (Nullable + ImplicitUsings)

**Primary Dependencies**: ModelContextProtocol SDK (MapMcp + ConfigureSessionOptions + WithListToolsHandler), OpenIddict (AgentPlatform IdP), Wolverine, YARP (gateway rotaları)

**Storage**: Yok — DB/şema değişikliği sıfır; tümü endpoint/oturum/config katmanı

**Testing**: xUnit + Shouldly; saf budama/kesişim mantığı test-first (İLKE VI), endpoint/wiring canlı doğrulama (quickstart)

**Target Platform**: Aspire AppHost (tüm sistem) + AgentPlatform reposu (IdP değişikliği AYRI PR)

**Project Type**: Mevcut mikroservis çözümünde yüzey refactor'u (yeni proje yok)

**Performance Goals**: tools/list p95 bugünkü fasat davranışıyla eşdeğer (scope-parmakizi cache; kullanıcı başına değil scope-seti başına toplama)

**Constraints**: Anonim catalog/stock `/mcp` keşif seti DEĞİŞMEZ (070); `RequireLoginUpfront=true` + uyuyan step-up bayrağı korunur; fasat→platform taşıma (dilim B) kapsam dışı

**Scale/Scope**: 33 admin tool (catalog 22, stock 3, customer 5, discount 3), 5 servis + gateway config + AgentPlatform 2 dosya; yeni endpoint kontratı yok, mevcut kontrat DARALIYOR

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Durum | Not |
|---|---|---|
| I — BC izolasyonu | PASS | DB/model paylaşımı yok; değişen yalnız her BC'nin kendi transport ucu. `Shared/McpToolNames` zaten sanksiyonlu sözleşme. |
| II — Zengin aggregate | PASS (n/a) | Domain'e dokunulmuyor; tool handler'ları aynen. |
| III — VSA/CQRS | PASS | Slice'lar değişmiyor; budama Program.cs/holder katmanında. Politika holder'ları (`*AdminSurface`) korunur, Program.cs orkestrasyon kalır. |
| IV — Result pattern | PASS (n/a) | Yeni handler yok. |
| V — Scope yetki | PASS | Zorlama scope kalır; rol görünmez. İstemci-tavanı OpenIddict izin-validasyonundan `ScopeResolver` kesişimine taşınır — duvar YER değiştirir, kalkmaz (bkz. research R3). |
| VI — Domain-TDD | PASS | Saf birimler test-first: scope-budama filtresi + `ScopeResolver` istemci-tavanı genişletmesi. Endpoint/wiring kapsam dışı. |
| VII — FLOW.md | PASS | Mağaza BC'lerinin domain süreci değişmiyor. AgentPlatform `Identity.Server/FLOW.md` scope-açılım adımı değişirse AYNI PR'da güncellenir. |

Post-design re-check: PASS — ihlal yok, Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/085-single-mcp-surface/
├── plan.md              # Bu dosya
├── research.md          # Faz 0 — kararlar R1-R6
├── quickstart.md        # Faz 1 — canlı doğrulama senaryoları
├── contracts/
│   └── mcp-surface.md   # Tek-uç yüzey kontratı (endpoint + PRM + budama kuralları)
└── tasks.md             # /speckit-tasks üretir (bu komut DEĞİL)
```

`data-model.md` ÜRETİLMEDİ — yeni entity/tablo/aggregate yok (anayasa "boş-doğru dosya üretme" kuralı).

### Source Code (repository root)

```text
src/agents/Mcp.Gateway/
├── Program.cs                        # /mcp-admin MapMcp + ikinci PRM ölür; challenge tek scope listesi
├── FacadeScopes.cs                   # Customer/Admin demetleri → tek union + PRM ilanı
├── Options/FacadeOption.cs           # DownstreamBc.Surface alanı ölür; BC başına TEK entry
├── Routing/SurfaceFilter.cs          # ÖLÜR (yol→yüzey çevirisi kalkıyor)
├── Aggregation/ToolCatalogCollector.cs  # cache anahtarı: surface → scope-parmakizi; keşif token seçimi
├── Aggregation/ProxyToolInvoker.cs   # registry tam-katalog (m2m) üzerinden; davranış aynı
├── Auth/McpStepUpMiddleware.cs       # /mcp-admin dalı sadeleşir (uyuyan kod, minimal dokunuş)
└── appsettings*.json                 # Downstreams: *-admin entry'leri ölür

src/services/{catalog,stock,customer,discount}/*/Program.cs   # ikinci MapMcp + AddMcpAdminResourceMetadata ölür;
                                                              # ConfigureSessionOptions yol-prefix → scope okur
src/services/*/​Mcp/*AdminSurface.cs   # ToolNames kalır; yanına RequiredScope eşlemesi (tool→scope)
src/others/Common/Extensions/McpResourceMetadataExtension.cs  # AddMcpAdminResourceMetadata emekli/uyarlanır
src/services/gateway/Gateway/appsettings*.json                # /mcp-admin/{service} rotaları + PRM kayıtları ölür

tests/Mcp.Gateway.Tests/              # SurfaceFilterTests ölür; ScopePruning testleri doğar

../AgentPlatform/src/Identity.Server/ (AYRI PR)
├── Connect/AuthorizeEndpoint.cs      # istemci-tavanı kesişimi (ScopeResolver'a clientCeiling)
├── Rbac/ScopeResolver.cs             # Resolve(requested, roleBundle, clientCeiling, alwaysAllow)
└── Program.cs                        # OpenIddict scope-izin ön-validasyonu gevşetilir (research R3)
```

**Structure Decision**: Mevcut çözüm yapısı korunur; yeni proje/klasör yok. Tek çapraz-repo dokunuş AgentPlatform (2-3 dosya, ayrı PR, önce merge edilir — ECommerce tarafı ona bağımlı DEĞİL, union PRM açılana dek eski davranış çalışır).

## Complexity Tracking

İhlal yok — tablo boş.
