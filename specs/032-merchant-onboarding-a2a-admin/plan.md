# Implementation Plan: Admin — Metinle Merchant Onboarding (back-end MCP routing)

**Branch**: `032-merchant-onboarding-a2a-admin` | **Date**: 2026-08-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/032-merchant-onboarding-a2a-admin/spec.md`

## Summary

ECommerce'e **admin-only metin (chat) yüzeyi** eklenir; akış **back-end MCP routing** ile kontrol edilir.
**WebApp** bir **onboarding MCP yüzeyi** açar: `submit_registration` (içeride mevcut
`GatewayRegistrationClient.RegisterAsync`'i sarar → iki-adım + challenge yayını aynı process'te çözülür)
+ `registration_status`. **ChatAgent**'a 3. bir **`admin` persona** (`AddAIAgent("admin")`) + kendi
prompt'u eklenir; admin'in metnini bu WebApp MCP tool'larına yönlendirir. **WebApp**'e admin-korumalı
**Razor ekranı** + BFF proxy admin kolu eklenir. Gateway'in Merchant.Agent A2A'sı KULLANILMAZ (router
bizde). Mevcut `POST /gateway-onboarding/register` yapısal yolu coexist. **Yeni proje yok.**

## Technical Context

**Language/Version**: C# / .NET (mevcut çözüm; Nullable + ImplicitUsings açık)

**Primary Dependencies**: `ModelContextProtocol` (WebApp MCP server hosting + ChatAgent MCP client —
mevcut McpServers/McpClients deseni), Microsoft Agent Framework (`AddAIAgent`, `ChatClientAgent`), ASP.NET
Razor Pages (WebApp), OIDC cookie auth, Aspire. Mevcut `GatewayRegistrationClient` (MCP client → gateway).

**Storage**: Yok (yeni kalıcı domain yok). Başvuru gateway'de (Merchant.Api) kalıcılaşır. Challenge
geçici olarak WebApp `IChallengeStore` (mevcut, in-memory).

**Testing**: E2E (Playwright) admin onboarding ekran akışı. Saf domain birim testi YOK (yeni domain yok).
LLM tool-seçimi E2E dışı.

**Target Platform**: Aspire ile ECommerce çözümü (WebApp + ChatAgent orchestrator + Identity.Server) +
ayrı DropShop gateway (Merchant.Api).

**Project Type**: Web (WebApp Razor BFF + MCP server) + agent orchestrator (ChatAgent).

**Performance Goals**: Yok (etkileşimli chat).

**Constraints**: Onboarding MCP yalnız `admin` persona; shopper/anonim'e sızmaz. Gateway erişilemezse
graceful-degrade (tool yok, boot çökmez). Config Options pattern. Challenge iki-adımı **WebApp process'inde**
(challenge-locality). Gateway çağrısı makine kimliğiyle.

**Scale/Scope**: Küçük-orta. WebApp: 1 MCP yüzeyi (2 tool) + 1 Razor ekran + 1 BFF proxy kolu + Options.
ChatAgent: 1 persona + 1 prompt + MCP client kaydı.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

- **I. BC İzolasyonu** ✅ ECommerce gateway'e yalnız **istemci** (MCP); gateway DB/aggregate'ine dokunmaz. Yeni BC yok.
- **II. Zengin Aggregate** ➖ Uygulanamaz — yeni aggregate/domain mantığı yok (UI + agent-config + MCP wrapper).
- **III. Vertical Slice + CQRS** ✅ WebApp MCP tool + Razor ekran + ChatAgent persona mevcut slice/desenlere
  (GatewayOnboarding, ChatEndpoints, public/assistant persona) eklenir; repository yok.
- **IV. Result Pattern** ➖ Yeni domain sonucu yok; MCP tool mevcut `GatewayRegistrationClient` sonucunu sarar.
- **V. Scope-Öncelikli Yetki + Rol=Scope** ✅ (nüans, research D1): Admin ekranı + BFF proxy **WebApp cookie-UI
  yüzeyidir** (mevcut `_Layout` `User.IsInRole("admin")` deseni; IdP-admin-UI istisnası mantığı) → rol-cookie ile
  korunur. WebApp onboarding MCP yüzeyi admin ile korunur. Gateway'e giden asıl çağrı **makine kimliğiyle**
  (`ecommerce-onboarding` client_credentials, "makine kimlikleri RBAC dışı"). Downstream gateway API'sinde
  ECommerce tarafı rol-policy koymaz; gateway kendi scope'unu (merchant.write) zorlar.
- **VI. Domain-TDD** ➖ Saf domain mantığı yok → TDD dışı; doğrulama E2E + quickstart.

**Sonuç**: Gate PASS. II/IV/VI uygulanamaz (yeni domain yok); V nüansı research D1'de gerekçelendirildi.

## Project Structure

### Documentation (this feature)

```text
specs/032-merchant-onboarding-a2a-admin/
├── plan.md · research.md · data-model.md · quickstart.md · contracts/ · tasks.md (/speckit-tasks üretir)
```

### Source Code (repository root)

```text
src/ui/WebApp/
├── GatewayOnboarding/
│   ├── GatewayRegistrationClient.cs      # + StatusAsync(domain) (registration_status MCP çağrısı); RegisterAsync değişmez
│   ├── OnboardingMcpTools.cs             # YENİ — MCP yüzeyi: submit_registration (RegisterAsync sarar) + registration_status
│   └── GatewayOnboardingEndpoints.cs     # DEĞİŞMEZ (POST /gateway-onboarding/register coexist)
├── Options/
│   ├── DropShopGatewayOption.cs          # mevcut (032-prep)
│   └── (gerekirse) WebApp MCP url'i ilgili Options'a alan
├── Extensions/OptionsExt.cs              # mevcut bind + gerekli ek
├── Chat/ChatEndpoints.cs                 # + admin kolu: admin rolünde `admin` persona'ya proxy
├── Pages/Admin/Onboarding.cshtml(.cs)    # YENİ — admin-korumalı metin ekranı ([Authorize(Roles="admin")])
└── Program.cs                            # + MCP server host kaydı (WithToolsFromAssembly / MapMcp), admin-korumalı

src/agents/ChatAgent/
├── ConstValues.cs                        # + WebApp onboarding MCP server sabiti (McpServers.Onboarding, allowedTools) + Prompts.AdminOnboardingInstructions
└── Program.cs                            # + adminAgentTools (onboarding MCP) + AddAIAgent("admin", ... CollectTools)
```

**Structure Decision**: Mevcut MCP server/client altyapısı (McpServers/McpClients.WithToken), ChatAgent
persona deseni (public/assistant → +admin), WebApp BFF (ChatEndpoints + Razor Pages) ve
`GatewayRegistrationClient` yeniden kullanılır. **Yeni proje/csproj YOK.** WebApp MCP tool'u
`GatewayRegistrationClient`'i sararak challenge-locality'yi çözer (register WebApp process'inde koşar).

## Revizyon (implement, kullanıcı kararı)

- **WebApp'te MCP YOK.** Onboarding MCP yüzeyi + `GatewayRegistrationClient.StatusAsync` iptal.
  ChatAgent `admin` persona doğrudan **DropShop Merchant.Api `/mcp`**'yi toplar (submit_registration +
  registration_status), makine token'ıyla (`ecommerce-onboarding` client_credentials). WebApp yalnız
  admin ekran (`Pages/Admin/Onboarding`) + BFF kolu (`/chat/admin/stream`, admin-cookie).
- **Challenge KALDIRILDI** (DropShop, ayrı repo): `submit` → doğrudan Pending → insan Approve/Reject.
  Challenge-locality gerekçesi (D4) düştü; WebApp challenge REST'i (dead) coexist kalır.
- **Metin akışında MCP dolaylı** (CLAUDE.md kuralı): elle `CallToolAsync` yok; tool + prompt.

## Complexity Tracking

Yok — Constitution Check ihlalsiz geçti.