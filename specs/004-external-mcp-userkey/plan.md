# Implementation Plan: External MCP UserKey

**Branch**: `004-external-mcp-userkey` | **Date**: 2026-07-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-external-mcp-userkey/spec.md`

## Summary

Dış tüketiciler MCP'yi tek bir kalıcı opak `UserKey` ile kullanır. Anahtar Identity.Server'da
`ApiKeys` satırı olarak (hash'li) bir kullanıcıya bağlanır, süresizdir, on-demand iptal edilir.
Yetki **kullanıcıya** bağlıdır: kullanıcı kayıtta operatör-tanımlı listeden scope seçer, seçim
`UserScopes` tablosunda tutulur; anahtar bu scope'ları miras alır (rol yok — anayasa V). Servisler
JWT-olmayan bir custom **ApiKey authentication şeması** ile anahtarı Identity.Server'ın resolve
uç noktasından çözer, kullanıcı principal'ini + UserScopes'unu kurar. Okumalar anonim; yazmalar
anahtar ister. Gateway `/mcp`'de değişmeden pass-through kalır; yetki yine scope-tabanlı.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Duende IdentityServer, ASP.NET Core Authentication, EF Core
(Identity.Server), Marten + Wolverine (servisler), ModelContextProtocol (MCP)

**Storage**: Identity.Server `ApplicationDbContext` (Postgres, EF Core) — yeni `ApiKeys` tablosu.
Servisler kendi DB'lerine dokunmaz; anahtar sahipliği yalnızca Identity.Server'da.

**Testing**: xUnit + Shouldly. Anahtar üretim/hash/çözümleme birim testleri; auth handler davranışı.

**Target Platform**: Aspire ile orkestre edilen Linux/container servisleri; Identity.Server HTTPS.

**Project Type**: Dağıtık mikroservis (web-service) + Identity.Server + YARP gateway.

**Performance Goals**: Anahtar yalnızca yazmalarda çözülür (seyrek); resolve çağrısı yazma başına
tek iç hop. İptal ≤ 5 sn etkili (agresif cache yok).

**Constraints**: BC izolasyonu — servisler `IdentityDbContext`'e erişemez, resolve uç noktası
üzerinden konuşur. Anahtar ham saklanmaz (SHA-256 hash). Rol yok, yalnızca scope.

**Scale/Scope**: Küçük operatör seti anahtar yönetir; on/yüzler mertebesinde dış anahtar beklenir.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar bakılır.*

- **I. Bounded Context İzolasyonu** ✅ — `ApiKeys` yalnızca Identity.Server'da; servisler
  HTTP resolve kontratıyla konuşur, tabloya/DbContext'e dokunmaz. Ortak şey yalnızca kontrat.
- **II. Zengin Aggregate** ⚠️ N/A — `ApiKey` bir Marten domain aggregate'i değil, Identity.Server'ın
  EF Core altyapı entity'sidir (mevcut `ApplicationUser`/Identity tabloları gibi). Marten aggregate
  kuralları buraya uygulanmaz; yine de küçük davranış (Create/Revoke) entity üstünde toplanır.
- **III. Vertical Slice + CQRS** ✅ — Servis tarafında yeni slice yok; custom auth ortak infra
  (`Common`), mevcut `AuthenticationExtension` gibi. Identity.Server uçları minimal API.
- **IV. Result Pattern** ✅ — Servis kodu değişmez. Auth handler framework tipi (`AuthenticateResult`)
  döner (çerçeve sözleşmesi). Identity.Server uçları uygun HTTP status döner.
- **V. Scope-Tabanlı Yetki (Rol Yok)** ✅ (amendment sonrası) — Authentication'a **ikinci bir
  şema** (JWT-olmayan ApiKey) eklenir; **yetki hâlâ scope-tabanlı**, rol yok. Anayasa **v1.1.1**
  PATCH amendment'ı JWT-olmayan custom şemaları açıkça meşrulaştırdı → çelişki giderildi.

**Post-Design re-check (Faz 1 sonrası)**: Tasarım İlke V'i güçlendirdi — yetki `UserScopes` ile
**kullanıcıya bağlı scope**, RBAC/rol **yok** (kullanıcının rol fantezisi scope-only'ye oturtuldu).
Tek açık deviation ikinci auth şeması; diğer tüm gate'ler ✅. Yeni gate ihlali yok.

## Project Structure

### Documentation (this feature)

```text
specs/004-external-mcp-userkey/
├── plan.md              # Bu dosya
├── research.md          # Faz 0 — tasarım kararları
├── data-model.md        # Faz 1 — ApiKey entity
├── quickstart.md        # Faz 1 — uçtan uca doğrulama
├── contracts/           # Faz 1 — resolve + admin uç kontratları
│   ├── resolve-key.md
│   └── manage-keys.md
└── tasks.md             # /speckit-tasks çıktısı (bu komut üretmez)
```

### Source Code (repository root)

```text
src/others/Identity.Server/
├── Data/ApiKey.cs                         # yeni EF entity (hash, userId, revoked) — scope taşımaz
├── Data/UserScope.cs                      # yeni EF entity (userId, scope) — kayıtta seçilir
├── Data/ApplicationDbContext.cs           # DbSet<ApiKey> + DbSet<UserScope> + config
├── Data/Migrations/ApplicationDb/…        # yeni migration (ApiKeys + UserScopes tabloları)
├── ApiKeys/ApiKeyEndpoints.cs             # /api/keys/resolve + admin issue/revoke (minimal API)
├── ApiKeys/ApiKeyService.cs               # üretim + SHA-256 hash + çözümleme (+ UserScopes okuma)
├── Pages/Account/Create/…                 # kayıt ekranı: operatör-tanımlı scope seçimi (checkbox)
└── Config.cs                              # apikeys.manage ApiScope + operatör-sunulan liste + admin client grant

src/others/Common/
├── Auths/ApiKeyAuthenticationHandler.cs   # X-User-Key → resolve → ClaimsPrincipal
├── Auths/ApiKeyAuthenticationOptions.cs   # şema seçenekleri (resolve adresi, header adı)
├── Extensions/AuthenticationExtension.cs  # ApiKey şeması + forward "smart" policy scheme
└── Middleware/…                           # present-but-invalid key → 401 (anonim read'lerde de)

# Servisler: AddAuthenticationAndAuthorizationExtension zaten çağrılıyor → forward şeması
# devreye girer; write MCP tool'ları/command'ları [RequiredScope] ile korunmaya devam eder.
# Okuma yüzeyi anonim: read query'lerde [RequiredScope] yoktur, /mcp gateway route'u auth'suz.
```

**Structure Decision**: Değişiklikler iki yerde toplanır — (1) **Identity.Server**: `ApiKeys`
kalıcılığı + resolve/admin uçları; (2) **Common**: paylaşılan custom auth şeması + forward policy.
Tek tek servis kodları değişmez (yalnızca ortak infra üzerinden şema kazanırlar). Gateway sabittir.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| İkinci authentication şeması (JWT-olmayan ApiKey), İlke V "JWT bearer" ifadesine ek | Dış tüketici OAuth dansı/expiration istemiyor; kalıcı opak anahtar + iptal şart | Saf JWT: expiration + OAuth flow getirir (kullanıcı reddetti); reference token: yine flow + exp |
| `ApiKey` Marten aggregate değil, EF entity (İlke II dışı) | Identity.Server Duende gereği EF Core kullanır; anahtar↔kullanıcı eşlemesi orada yaşamalı (BC) | Marten aggregate'i servis tarafında: anahtar sahipliğini Identity dışına taşır, BC'yi bozar |