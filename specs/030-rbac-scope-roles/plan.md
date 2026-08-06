# Implementation Plan: RBAC — Rol = Scope Demeti

**Branch**: `030-rbac-scope-roles` | **Date**: 2026-08-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/030-rbac-scope-roles/spec.md`

## Summary

Rol tabanlı yetkilendirme, rolü **token verme anında bir scope demetine açarak** ekler.
Bugün `AuthorizeEndpoint` BFF ne isterse veriyor; bu feature granted API scope'larını
kullanıcının rol demetiyle **süzer** (`granted = requested ∩ roleBundle`). Roller ve
rol→scope map DB'de (ASP.NET Identity `AspNetRoles` + yeni `RoleScopes` tablosu); atanabilir
scope'lar KOD-sahipli kapalı registry'den (`KnownScopes`) gelir — admin ekrandan yalnız bu
listeden seçer (serbest metin yok). Admin yönetim ekranı IdP Razor Pages'te; giriş WebApp
header'ındaki koşullu link. Seed: admin+customer rolleri, rol→scope map, bootstrap admin,
ingestion-agent client. Downstream servisler DEĞİŞMEZ — yalnız scope görürler.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: OpenIddict (server + EF Core stores), ASP.NET Identity
(`AddIdentity<ApplicationUser, IdentityRole>` zaten kayıtlı), EF Core + Npgsql, Razor Pages.
Common (`AuthorizationScopes`, `AuthenticationExtension`, `ScopeAuthorizationMiddleware`).

**Storage**: Postgres `identityDb` (EF Core — Marten DEĞİL). Yeni `RoleScopes` tablosu +
migration; `AspNetRoles`/`AspNetUserRoles` zaten mevcut (IdentityDbContext üretir).

**Testing**: xUnit + Shouldly. Saf birimler test-first (İlke VI): `ResolveGrantedScopes`,
`ValidateAssignableScope`, `ApplySingleRole`.

**Target Platform**: Aspire-hosted web service (Identity.Server) + server-rendered admin UI;
HTTPS zorunlu (issuer eşleşmesi).

**Project Type**: Web service (IdP) + Razor Pages admin UI + tek küçük WebApp (header link) değişikliği.

**Performance Goals**: Token verme yolu ek bir DB okuması (kullanıcı rolü → rol scope'ları)
ekler; login sıklığında, kritik değil. Kısa access-token ömrü + refresh.

**Constraints**: Downstream scope-zorlama kodu değişmez; scope claim'i array kalır
(`ScopeClaimArrayHandler`); rol asla downstream'e yetki için sızmaz; bootstrap parola kodda değil.

**Scale/Scope**: Az rol (2 seed + admin-tanımlı birkaç), ~14 scope, kullanıcı başına tek rol.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakıldı.*

Anayasa v1.7.0. **Not**: Identity.Server bir domain BC değil, IdP altyapısıdır — İlke II/III/IV
(zengin aggregate / Marten-Wolverine vertical slice / Common.Results) domain servisleri için
geçerlidir; IdP EF Core + OpenIddict + Razor Pages ile 029'dan beri bu desenin dışındadır.
Bu feature o gerçekliği değiştirmez, yeni ihlal getirmez.

| İlke | Durum | Not |
|------|-------|-----|
| I. BC İzolasyonu | ✅ PASS | Rol yalnız IdP'de; downstream yalnız scope görür; hiçbir servis rol taksonomisini bilmez. |
| II. Zengin Aggregate | ➖ N/A | IdP altyapısı; Role/RoleScope config verisidir, domain aggregate değil (İlke II domain BC'leri kapsar). |
| III. Vertical Slice/CQRS/Repo-yok | ➖ N/A | IdP Razor Pages + OpenIddict endpoint deseni (mevcut). Marten/Wolverine slice değil; 029 gerçekliği sürer. |
| IV. Result Pattern | ➖ N/A | IdP OIDC handler'ları/pages `FeatureResultModel` kullanmaz (mevcut durum); yeni servis değil. |
| V. Scope-Öncelikli + Rol=Scope Demeti | ✅ PASS | Feature'ın özü. Rol token-anında scope'a açılır; KnownScopes kapalı registry; DB map + admin ekran; makine client_credentials. |
| VI. Domain-TDD | ✅ PASS | Saf birimler (scope çözümleme, scope doğrulama, tek-rol) test-first; tasks'ta test task'ı önce. |

**İlke V incelik (D3)**: IdP admin Razor Pages'i cookie kullanıcısının `admin` rolüyle korunur
(JWT scope yerine), çünkü rol otoritesinin KENDİ iç yüzeyidir; cookie'de scope claim'i yoktur.
`identity.roles.manage` scope'u programatik guard olarak tanımlı ve admin demetinde. Bu gerginlik
anayasa **v1.7.1** ile İlke V'e açıklayıcı istisna olarak eklendi (030-rbac analyze K1) — kayıtta
çözülü, dilution değil.

**Sonuç**: GATE geçildi (Phase 0 ve Phase 1 sonrası). Complexity Tracking gerekmez.

## Project Structure

### Documentation (this feature)

```text
specs/030-rbac-scope-roles/
├── plan.md              # Bu dosya
├── spec.md              # Feature spec
├── research.md          # D1–D6 kararlar
├── data-model.md        # Role/RoleScope/KnownScope + invariant'lar
├── quickstart.md        # S0–S7 canlı doğrulama
├── contracts/
│   └── role-management.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
src/others/Identity.Server/
├── Config.cs                         # KnownScopes + açıklamalar; ingestion-agent client; seed scope map kaynağı
├── SeedHostedService.cs              # + admin/customer rol, rol→scope map, bootstrap admin, backfill (idempotent)
├── Rbac/                             # YENİ — RBAC çekirdeği
│   ├── RoleScope.cs                  # EF entity (RoleId, Scope)
│   ├── KnownScopes.cs                # kod registry (ad + açıklama)
│   ├── ScopeResolver.cs              # saf: ResolveGrantedScopes(requested, bundle, known)
│   ├── RoleAssignmentService.cs      # tek-rol atama, son-admin kilidi, rol/scope yönetimi
│   └── AssignableScopeValidator.cs   # saf: ValidateAssignableScope
├── Data/ApplicationDbContext.cs      # + DbSet<RoleScope> + unique(RoleId,Scope)
├── Endpoints/AuthorizeEndpoint.cs    # SetScopes → rol demetiyle süz (D1)
├── Endpoints/TokenEndpoint.cs        # refresh dalında scope'ları güncel rolden yeniden türet
├── Pages/Account/Create/Index.cshtml.cs   # register → otomatik customer rolü
└── Pages/Admin/                      # YENİ — cookie+admin korumalı yönetim UI
    ├── Roles/ (List, Create, Delete, Scopes)
    └── Users/ (List, SetRole)

src/others/Common/Utils/Constants/AuthorizationScopes.cs   # + identity.roles.manage sabiti

src/ui/WebApp/                         # header'a scope-koşullu "Yönetim" linki (D6)

tests/Identity.Server.Tests/           # YENİ (veya mevcut) — saf birim testleri (İlke VI)
```

**Structure Decision**: Yeni proje/servis AÇILMAZ. Tüm değişiklik Identity.Server içinde +
Common'da tek scope sabiti + WebApp'te tek header linki. RBAC çekirdeği `Identity.Server/Rbac/`
altında; saf birimler (ScopeResolver, AssignableScopeValidator, tek-rol dönüşümü) ayrı sınıflarda,
IdP altyapısından bağımsız test edilebilir tutulur.

## Complexity Tracking

Gerekmez — Constitution Check'te gerekçelendirilecek ihlal yok (IdP zaten İlke II/III/IV
kapsamı dışı; bu feature yeni sapma getirmez).