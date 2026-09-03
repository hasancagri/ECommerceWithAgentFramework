# Implementation Plan: Dış Agent MCP Erişimi (OAuth)

**Branch**: `061-external-mcp-oauth` | **Date**: 2026-09-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/061-external-mcp-oauth/spec.md`

## Summary

Dış agent'lar (Claude Code/Desktop) mağaza MCP uçlarına OAuth 2.1 ile bağlanır: korumalı MCP
uçları `401 + resource_metadata` ile keşif verir (RFC 9728), Identity.Server'a RFC 7591 DCR ucu +
tek consent sayfası + revocation eklenir; token = mevcut JwtBearer zinciri (yeni doğrulama katmanı
YOK). UserKey yolu dokunulmadan yan yolda kalır. Canlı kanıt: Claude Code'dan localhost'a bağlan,
bir kez tarayıcıda login/consent, sonra yazışmayla ara→sepet→sipariş→görüntüle.

## Technical Context

**Language/Version**: .NET 10 / C# (Nullable + ImplicitUsings)

**Primary Dependencies**: OpenIddict 7.6 (IdP), ModelContextProtocol server paketi (mevcut MapMcp),
ASP.NET JwtBearer, YARP (gateway), Wolverine (değişmiyor)

**Storage**: identityDb (OpenIddict application/authorization/token tabloları — mevcut şema;
yeni domain tablosu YOK)

**Testing**: xUnit + Shouldly (saf birimler: DCR redirect-URI/scope doğrulayıcısı); asıl kanıt
canlı E2E (quickstart.md)

**Target Platform**: Aspire AppHost (localhost); Identity.Server HTTPS, servisler http localhost

**Project Type**: Mevcut mikroservis çözümüne yatay auth dilimi (Common + Identity.Server +
gateway + 4 servis Program.cs)

**Performance Goals**: Yok (auth seremonisi insan-hızı; tool çağrısı mevcut yol)

**Constraints**: Mevcut kanallar regresyonsuz (ChatAgent PUBLIC/ASSISTANT, WebApp BFF, UserKey);
storefront/catalog/stock MCP anonim kalır; PAN/secret LLM bağlamına giremez

**Scale/Scope**: ~6 proje dokunulur; yeni servis YOK, yeni aggregate YOK

## Constitution Check

*GATE: Phase 0 öncesi değerlendirildi; Phase 1 sonrası yeniden kontrol edildi — İHLAL YOK.*

- **İlke I (BC izolasyonu)**: UYUMLU. DB paylaşımı yok; değişiklik auth/transport katmanında.
  Identity.Server zaten merkezi IdP. MCP'yi yine yalnız agent tüketir (Claude = agent istemcisi).
- **İlke II (zengin aggregate)**: KAPSAM DIŞI-UYUMLU. Yeni aggregate yok; OpenIddict kendi
  application/token modelini yönetir (framework-içi, domain modeli değil).
- **İlke III (VSA+CQRS)**: UYUMLU. DCR/metadata uçları domain slice değil auth altyapısı;
  Identity.Server'ın mevcut Connect yapısına eklenir, Common extension deseni korunur.
- **İlke IV (Result)**: UYUMLU. Auth uçları OAuth/RFC hata biçimleri döner (protokol kontratı);
  domain handler'lara dokunulmuyor.
- **İlke V (scope yetki)**: GÜÇLENIYOR. Zorlama scope-bazlı kalır; dış agent demeti kapalı
  registry'den (`AuthorizationScopes`) seçildi; rol downstream'e sızmaz; DCR client'ları
  client_credentials alamaz. Anonim gezinme yüzeyleri anonim kalır.
- **İlke VI (Domain-TDD)**: DAR UYGULAMA. Saf birim: DCR istek doğrulayıcısı (redirect URI +
  scope süzgeci) test-first; geri kalan wiring/endpoint kapsam dışı (test-sonra/canlı).
- **İlke VII (FLOW.md)**: TETİKLENMEZ. Domain süreci (command-event-policy) değişmiyor; auth
  transport katmanı. FLOW güncellemesi yok.

## Project Structure

### Documentation (this feature)

```text
specs/061-external-mcp-oauth/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 — kararlar R1–R8
├── data-model.md        # Phase 1 — OpenIddict varlık kullanımı + DCR kayıt modeli
├── quickstart.md        # Phase 1 — Claude Code bağlanma + E2E canlı doğrulama
├── contracts/
│   ├── protected-resource-metadata.md   # RFC 9728 doküman + 401 challenge kontratı
│   └── dcr-register.md                  # RFC 7591 register ucu kontratı
└── tasks.md             # /speckit-tasks üretir (bu komut değil)
```

### Source Code (repository root)

```text
src/others/Common/
├── Auths/                       # (mevcut) ApiKey* dokunulmaz
└── Extensions/
    └── AuthenticationExtension.cs   # + Bearer challenge'a resource_metadata parametresi
    └── McpResourceMetadataExtension.cs  # YENİ: /.well-known/oauth-protected-resource ucu

src/others/Identity.Server/
├── Connect/
│   ├── SeedHostedService.cs     # (mevcut) seed client'lar Implicit kalır
│   └── RegisterEndpoint.cs      # YENİ: RFC 7591 DCR (+ saf doğrulayıcı sınıf)
├── Pages|Views (mevcut yapıya göre)
│   └── Consent sayfası          # YENİ: tek sayfa Onayla/Reddet
└── Program.cs                   # + revocation endpoint, + discovery'ye registration_endpoint

src/services/gateway/Gateway/appsettings*.json  # + well-known route'ları (4 korumalı servis)

src/services/{basket,order,customer,payment}/*/Program.cs
                                 # MapMcp("/mcp").RequireAuthorization() + metadata ucu aç

tests/Identity.Server.Tests/     # YENİ (yoksa): DCR doğrulayıcı birim testleri (test-first)
```

**Structure Decision**: Yatay dilim — yeni proje açılmaz. Keşif yeteneği `Common`'da tek yerde
yazılır (FR-002), 4 korumalı servis Program.cs'ten açar; storefront/catalog/stock MCP'ye
DOKUNULMAZ (anonim kalır, R3). Identity.Server değişiklikleri kendi Connect/ yapısında.

## Complexity Tracking

İhlal yok — tablo boş.