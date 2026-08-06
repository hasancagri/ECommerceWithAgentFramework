# Implementation Plan: OpenIddict Migrasyonu (Davranış Birebir)

**Branch**: `029-openiddict-migration` | **Date**: 2026-08-06 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/029-openiddict-migration/spec.md`

## Summary

Identity.Server'daki Duende IdentityServer, OpenIddict 7.6.0 + mevcut ASP.NET Identity ile değiştirilir.
Token sözleşmesi (issuer, aud, çoklu scope claim'i, kullanıcı claim'leri) birebir korunur; hiçbir servis kodu değişmez.
3 client + 13 scope + 8 audience eşlemesi kod seed'ine taşınır; login/register/logout sayfaları SignInManager'a port edilir.
DB sıfırlanır (Docker volume reset): veri taşıma yok; tüm migration'lar silinip temiz tek Initial migration üretilir.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: OpenIddict.AspNetCore 7.6.0 + OpenIddict.EntityFrameworkCore 7.6.0 (yeni); çıkan: Duende.IdentityServer* 7.4.3.
Duende.IdentityModel 8.1.0 KALIR (Apache 2.0, ücretsiz; yalnız istemci yardımcıları — WebApp/servisler dokunulmaz).

**Storage**: Postgres `identityDb` (EF Core). Şema: Identity + ApiKeys + OpenIddict 4 tablosu; DB sıfırdan, temiz tek Initial migration.

**Testing**: xUnit + Shouldly. Saf domain mantığı yok (İlke VI kapsamı boş); doğrulama canlı smoke (quickstart.md).

**Target Platform**: Aspire AppHost altında `identity-server` resource'u; HTTPS https://localhost:5001 (launchSettings korunur).

**Project Type**: Mevcut web servisi (IdP) içi teknoloji takası; yeni proje yok.

**Performance Goals**: Bugünkü davranışla eşdeğer; ek hedef yok (dev ortamı).

**Constraints**: Sıfır servis değişikliği (SC-003); issuer/Authority birebir; access token düz JWT; scope claim'i çoklu-değer (Duende pariteli).

**Scale/Scope**: 1 proje yeniden yazımı (Identity.Server), ~3 client, 13 scope, 8 audience, 4 Razor sayfa grubu; servisler: 0 dosya.

## Constitution Check

*GATE: v1.6.0'a göre değerlendirildi — PASS (ihlal yok).*

- **İlke I (BC izolasyonu)**: Etkilenmez. Identity.Server BC değil, altyapı bileşeni; servisler arası kanal değişmiyor.
- **İlke II (zengin aggregate)**: Kapsam dışı — domain modeli yok; ApplicationUser/ApiKey mevcut haliyle kalır.
- **İlke III (VSA+CQRS)**: Kapsam dışı — IdP Razor Pages + OIDC uçları; mevcut ApiKeys endpoint düzeni korunur.
- **İlke IV (Result pattern)**: Kapsam dışı — OIDC protokol hataları standart protokol cevaplarıyla döner (bugünkü gibi).
- **İlke V (Scope-öncelikli + rol)**: Uyumlu. IdP teknoloji-nötr (OpenIddict hedefi anayasada); bu feature rol ATAMAZ (FR-011);
  scope zorlaması ve HTTPS/issuer kuralı birebir korunur; anonim gezinme sürer.
- **İlke VI (Domain-TDD)**: Saf domain birimi yok; test-first zorunluluğu doğmaz. Doğrulama canlı smoke.
- **Aspire/CPM/GlobalUsings kuralları**: Paket sürümleri Directory.Packages.props'a; sistem AppHost'tan koşulur; using'ler tek dosyada.

## Project Structure

### Documentation (this feature)

```text
specs/029-openiddict-migration/
├── plan.md              # Bu dosya
├── research.md          # Faz 0 — teknik kararlar ve riskler
├── data-model.md        # Faz 1 — DB tablo değişimi + seed modeli
├── quickstart.md        # Faz 1 — canlı smoke rehberi
├── contracts/
│   └── token-contract.md  # Faz 1 — token claim + client + endpoint sözleşmesi
└── tasks.md             # /speckit-tasks üretir (bu komut üretmez)
```

### Source Code (repository root)

```text
src/others/Identity.Server/
├── Program.cs                    # Duende kaydı → OpenIddict kaydı (server + EF store + seed hosted service)
├── Config.cs                     # Duende modelleri → düz seed sabitleri (client/scope/audience eşlemesi)
├── Identity.Server.csproj        # Duende.IdentityServer* çıkar, OpenIddict.* girer
├── Data/ApplicationDbContext.cs  # + UseOpenIddict(); yeni migration
├── Data/Migrations/PersistedGrantDb/  # SİLİNİR (Duende operational store)
├── keys/                         # SİLİNİR (Duende otomatik imza anahtarı)
├── Connect/                      # YENİ: authorize/token/userinfo/logout uç handler'ları
└── Pages/
    ├── Account/{Login,Logout,Create,AccessDenied}  # SignInManager'a port
    └── {Consent,Grants,Diagnostics,Ciba,Device,ServerSideSessions,ExternalLogin,Redirect}  # SİLİNİR

Directory.Packages.props           # sürüm değişimi (tek merkez)
```

**Structure Decision**: Tek proje içi takas; yeni proje/klasör yalnız `Connect/` (OIDC uçları). Servis projelerine dokunulmaz.

## Complexity Tracking

> İhlal yok — tablo boş.