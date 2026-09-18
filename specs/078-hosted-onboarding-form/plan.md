# Implementation Plan: Hosted Merchant Onboarding + Ekrandan Credential Teslimi

**Branch**: `078-hosted-onboarding-form` | **Date**: 2026-09-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/078-hosted-onboarding-form/spec.md`

## Summary

Onboarding PII'si (TCKN/IBAN) ve MerchantKey LLM sohbetinden çıkarılır. Store tarafında: (1)
`admin_start_onboarding` yalnız PG hosted form linki döndürür; (2) `admin_onboarding_status`
MerchantKey döndürmeyi bırakır; (3) yeni `admin_request_credential_entry_link` tool'u, Customer.Api'nin
kendi hosted credential-giriş ekranına süreli + tek kullanımlık link üretir; admin MerchantId+Key'i
ekrana elle girer, store kayıt anında PG'ye doğrular. Store↔PG kanalı imperatif MCP'den S2S REST'e
taşınır (anayasa sapması sökülür). PG tarafı (form, approve-mail, tek kullanımlık teslim sayfası,
REST uçları) ayrı repo işi; kontrat `contracts/pg-onboarding-rest.md`.

## Technical Context

**Language/Version**: .NET 10 / C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten (Postgres doc store), Wolverine (IMessageBus), MCP C# SDK
(`[McpServerToolType]`), ASP.NET Minimal API, xUnit + Shouldly

**Storage**: customerDb (Marten) — yeni `CredentialEntrySession` dokümanı + mevcut
`MerchantInformation`, `AdminActionLog`

**Testing**: xUnit + Shouldly; saf domain (CredentialEntrySession davranışı) test-first (İLKE VI)

**Target Platform**: Aspire AppHost altında Customer.Api; PG (DropShop) ayrı solution
(Identity 5101, Merchant.Api 5202, Gateway 5201)

**Project Type**: Mikroservis (mevcut Customer.Api BC'sine yüzey ekleme + dış realm kontratı)

**Performance Goals**: Yok (onboarding nadir, insan-hızlı akış); tek hedef link üretimi < birkaç sn

**Constraints**: PII/MerchantKey LLM transkriptine ve store loglarına girmez; ekran linki süreli +
tek kullanımlık; PG erişilemezken dostane hata; söküm ancak yeni akış canlı doğrulandıktan sonra

**Scale/Scope**: Tek merchant (mağazanın kendisi); ekran 1 sayfa; ~3 tool değişikliği + 1 yeni ekran +
1 REST istemci dönüşümü

## Constitution Check

*GATE: Phase 0 öncesi değerlendirildi; Phase 1 sonrası yeniden kontrol edildi — GEÇTİ (2 gerekçeli
sapma Complexity Tracking'de).*

- **İLKE I (BC izolasyonu)**: UYUMLU/İYİLEŞME — PG dış realm; kanal tipli S2S REST kontratına taşınır,
  DB izolasyonu değişmez. 070'in gerekçeli "agent-olmayan koddan imperatif MCP" sapması SÖKÜLÜR
  (gerekçesi olan "PG repo'suna dokunma yasağı" kalktı).
- **İLKE II (zengin aggregate)**: UYUMLU — `CredentialEntrySession` kimlikli, yaşam döngülü
  (Issued→Consumed/Expired), tek-kullanım invariant'ı `Consume()` davranışında.
- **İLKE III (VSA+CQRS)**: UYUMLU — ekran GET/POST Customer.Api'de kullanıcı-niyetli slice
  (`Features/Commands`); MCP tool'ları `Features/Agents/*` ince sarmalayıcı; repository yok.
- **İLKE IV (Result)**: UYUMLU — handler'lar Feature*ResultModel, aggregate ResultDomain;
  hata kodları `CustomerResourceConstants`.
- **İLKE V (scope yetki)**: UYUMLU (v1.11.1 istisnası) — credential ekranı, anayasanın "süreli
  tek-kullanımlık capability-link" istisnasına birebir uyar: linki `merchant.credentials.write`
  scope'lu tool üretir (zorlama üretim anında), token sunucu-durumlu + tek kullanımlık + kısa
  süreli, ekran yazma-only ve tek amaçlı.
- **İLKE VI (Domain-TDD)**: UYUMLU — `CredentialEntrySession.Create/Consume` testleri
  implementasyondan önce.
- **İLKE VII (FLOW.md)**: UYUMLU — customer FLOW.md onboarding süreci aynı PR'da güncellenir
  (yeni adımlar: link üret → ekran → doğrula-kaydet; eski chat-PII adımları silinir).
- **MCP-only müşteri/admin yüzeyi (CLAUDE.md doktrini)**: SAPMA-2 — store'a insan-yüzlü 1 hosted
  ekran ekleniyor (aşağıda gerekçeli).

## Project Structure

### Documentation (this feature)

```text
specs/078-hosted-onboarding-form/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   └── pg-onboarding-rest.md   # store↔PG REST kontratı + teslim-linki/mail semantiği
└── tasks.md             # /speckit-tasks üretir
```

### Source Code (repository root)

```text
src/services/customer/Customer.Api/
├── Domains/MerchantInformations/
│   ├── CredentialEntrySession.cs                  # YENİ aggregate (tek kullanımlık ekran oturumu)
│   ├── Features/Agents/Commands/
│   │   ├── AdminStartOnboarding.cs                # YENİ: PII'siz, PG form linki döner
│   │   ├── AdminRequestCredentialEntryLink.cs     # YENİ: ekran linki üretir
│   │   ├── AdminSubmitOnboarding.cs               # SÖKÜM (canlı doğrulama sonrası)
│   │   └── AdminSetMerchantCredentials.cs         # SÖKÜM (canlı doğrulama sonrası)
│   ├── Features/Agents/Queries/
│   │   └── AdminOnboardingStatus.cs               # DEĞİŞİR: MerchantKey alanı kalkar
│   └── Features/Commands/
│       └── SubmitMerchantCredentials.cs           # YENİ: ekran POST'u (doğrula + kaydet + iz)
├── CredentialEntryEndpointExtension.cs            # YENİ: GET ekran + POST kayıt (token-auth)
├── Onboarding/
│   ├── PgOnboardingClient.cs                      # YENİ: tipli REST istemci (MCP istemcisi yerine)
│   ├── MerchantOnboardingClient.cs                # SÖKÜM (imperatif MCP sapması)
│   ├── OnboardingGatewayTokenHandler.cs           # KALIR (aynen REST client'a takılır)
│   └── DropShopOnboardingOption.cs                # DEĞİŞİR: McpUrl → ApiBaseUrl
├── Options/CredentialEntryOptions.cs              # YENİ: PublicBaseUrl + LinkLifetime
├── FLOW.md                                        # DEĞİŞİR (İLKE VII, aynı PR)
tests/Customer.Api.Tests/
└── CredentialEntrySessionTests.cs                 # YENİ, test-first
```

**Structure Decision**: Tümü mevcut Customer.Api BC'si içinde — MerchantInformation zaten orada, yeni
BC/servis açılmaz. PG tarafı ayrı solution'da kontrata göre yapılır (bu repo işi değil).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| ~~SAPMA-1~~ ÇÖZÜLDÜ: anayasa v1.11.1 amendment'ı (İlke V capability-link istisnası, 2026-09-18) ekranı UYUMLU kıldı | Ekran tarayıcıda açılır; store'da web-login yüzeyi YOK (066'da söküldü) ve geri eklenmeyecek (clarify Q1=B) | OIDC interaktif login: web-login yüzeyini geri getirir; chat'ten giriş: borcun kendisi (key LLM'den geçer) |
| SAPMA-2 (CLAUDE.md MCP-only doktrini): store'a insan-yüzlü 1 hosted ekran | Gizli anahtar LLM transkriptine giremez; tek teslim yolu insan gözü + ekran (hosted ödeme sayfası emsali) | MCP tool'una key yazdırmak sohbete sızdırır; S2S auto-bind kullanıcı kararıyla ELENDİ (teslim insan-aracılı olacak) |