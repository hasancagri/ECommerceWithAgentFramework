# Implementation Plan: Wallet & AddressBook (Kayıtlı Kart + Adres Defteri)

**Branch**: `022-wallet-address-book` | **Date**: 2026-07-30 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/022-wallet-address-book/spec.md`

## Summary

Yeni **Customer BC** (servis + `customerDb`): iki aggregate root — `Wallet` (SavedCard
koleksiyonu) ve `AddressBook` (SavedAddress koleksiyonu), `UserId` ile keyli. Ekle/sil/
varsayılan-yap; "en fazla 1 varsayılan" invariant'ı aggregate metodunda korunur. SavedCard
ham PAN/CVV **taşımaz**; kart eklemede `ICardTokenizer` (bu iterasyonda simüle stub) token
üretir, yalnız token+marka+son4+son-kullanma+etiket saklanır. Okuma MCP'ye açık; yazma yalnız
REST/WebApp. Checkout snapshot yalnız **kontrat** olarak tanımlı (ayrı feature).

## Technical Context

**Language/Version**: .NET 10, C#, Nullable + ImplicitUsings açık

**Primary Dependencies**: Marten (document store), Wolverine (in-proc bus + RabbitMQ),
Duende IdentityServer (JWT bearer + scope), ModelContextProtocol (MCP server), Aspire

**Storage**: Postgres `customerDb`, Marten şeması `customerManagement`; iki document tipi
(`Wallet`, `AddressBook`), her ikisi `UserId` üzerinde index

**Testing**: xUnit + Shouldly; saf domain birim testleri (host harness yok) — invariant'lar

**Target Platform**: Linux/container servis, Aspire AppHost ile orkestre

**Project Type**: Mikroservis (yeni Bounded Context) + WebApp (Razor Pages BFF) UI

**Performance Goals**: Tekil kullanıcı CRUD; özel hedef yok (SC-001: adres < 30 sn tek form)

**Constraints**: PCI — ham PAN/CVV DB/log/event'te ASLA; tokenize başarısızsa fail-closed
(yarım kayıt yok); en fazla 1 varsayılan (eşzamanlıda bile)

**Scale/Scope**: Kullanıcı başına makul sayıda kart/adres (sert üst sınır yok)

## Constitution Check

*GATE: Phase 0 öncesi geçmeli. Phase 1 sonrası tekrar kontrol.*

- **I. BC İzolasyonu** ✅ — Yeni Customer BC; kendi DB (`customerDb`), kendi şema, kendi
  domain modeli. Address VO Customer BC'ye özel (Order'ınkinden ayrı tip; sızdırma yok).
  Bu feature'da servisler-arası kanal yok; checkout referansı ayrı feature'ın işi.
- **II. Zengin Aggregate** ✅ — `Wallet` + `AddressBook` iki ayrı aggregate root
  (`AggregateRoot`'tan türer); her biri kendi kimliği (UserId-keyli), invariant'ı (≤1
  varsayılan) ve yaşam döngüsü var. SavedCard/SavedAddress sade entity (base almaz).
  Koleksiyon private + IReadOnlyList; mutasyon yalnız aggregate metodundan.
- **III. Vertical Slice + CQRS** ✅ — Commands/ (ekle/sil/varsayılan) + Queries/ (listele)
  + Agent/ (MCP okuma). Repository yok; handler `IDocumentSession`. MCP tool = ince sarmalayıcı.
- **IV. Result Pattern** ✅ — Handler/aggregate/endpoint `FeatureResultModel` /
  `FeatureObjectResultModel<T>` / `FeatureListResultModel<T>` döner; `MessageItem.Code`
  resource sabiti. Doğrulama (boş adres, geçmiş son-kullanma, tokenize hatası) Result ile.
- **V. Scope-Tabanlı Yetki** ✅ — Yeni `customer.read` / `customer.write` scope'ları; rol yok.
  Endpoint `.RequireAuthorization(...)`. Kullanıcı `CurrentUser.Load(...)` ile; yalnız kendi
  UserId'sine erişir.

**Sonuç**: Tüm kapılar geçti; Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/022-wallet-address-book/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 çıktısı
├── data-model.md        # Phase 1 çıktısı
├── quickstart.md        # Phase 1 çıktısı
├── contracts/           # Phase 1 çıktısı (REST + tokenizer + MCP + snapshot kontratı)
└── tasks.md             # /speckit-tasks çıktısı (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/customer/Customer.Api/          # YENİ servis (Customer BC)
├── Customer.Api.csproj                       # Basket.Api.csproj ikizi (sürümsüz PackageReference)
├── GlobalUsings.cs
├── Program.cs                                # Marten(customerDb) + Wolverine + auth + MCP
├── Dependencies/DependencyExtensions.cs
├── Domains/
│   ├── Wallets/
│   │   ├── Wallet.cs                          # aggregate root + SavedCard entity + ≤1 varsayılan
│   │   ├── SavedCard.cs                       # sade entity (Token, Brand, Last4, Expiry, Label, IsDefault)
│   │   ├── WalletEndpointExtension.cs
│   │   ├── WalletMcpTools.cs                  # YALNIZ okuma (list_cards)
│   │   ├── Tokenization/
│   │   │   ├── ICardTokenizer.cs              # tokenize soyut kontratı
│   │   │   └── SimulatedCardTokenizer.cs      # stub (sahte token; gateway gelince swap)
│   │   └── Features/
│   │       ├── Commands/{AddCard,DeleteCard,SetDefaultCard}.cs
│   │       ├── Queries/GetCards.cs
│   │       └── Agent/GetCards.cs
│   └── AddressBooks/
│       ├── AddressBook.cs                     # aggregate root + SavedAddress entity + ≤1 varsayılan
│       ├── SavedAddress.cs                    # sade entity
│       ├── ValueObjects/Address.cs            # Customer BC'ye özel Address VO
│       ├── AddressBookEndpointExtension.cs
│       ├── AddressBookMcpTools.cs             # YALNIZ okuma (list_addresses)
│       └── Features/
│           ├── Commands/{AddAddress,UpdateAddress,DeleteAddress,SetDefaultAddress}.cs
│           ├── Queries/GetAddresses.cs
│           └── Agent/GetAddresses.cs

src/others/Shared/Utils/Constants/SchemaConstants.cs   # + CustomerSchemaName
src/others/Common/Utils/Constants/AuthorizationScopes.cs # + CustomerRead/CustomerWrite
src/others/Identity.Server/Config.cs                     # + customer scope/resource/BFF grant
src/aspire/AppHost/AppHost.cs                            # + customerDb + customer-api resource + WebApp/gateway ref
src/ui/WebApp/                                           # kart/adres yönetim sayfaları (BFF)

tests/Customer.Api.Tests/Customer.Api.Tests.csproj      # YENİ — Wallet + AddressBook invariant testleri
```

**Structure Decision**: Yeni mikroservis = yeni BC (`src/services/customer/Customer.Api`),
Basket.Api yapısını birebir yansıtır (Program.cs, GlobalUsings, Dependencies, Domains/Vertical
Slice). İki aggregate iki `Domains/` alt-klasörü. Fiziksel klasör = solution klasörü (constitution).

## Complexity Tracking

*Anayasa ihlali yok — boş.*