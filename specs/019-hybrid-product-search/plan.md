# Implementation Plan: Hibrit Ürün Araması (Filtre + Anlamsal, Sohbet Üzerinden)

**Branch**: `019-hybrid-product-search` | **Date**: 2026-07-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/019-hybrid-product-search/spec.md`

## Summary

Storefront BC'ye tek hibrit arama query'si eklenir: opsiyonel marka(OR)/fiyat/stok filtreleri +
opsiyonel anlamsal metin. Embedding'ler `ProductChangedEvent` akışında hash-değişiminde OpenAI
`text-embedding-3-small` ile üretilir, pgvector'lı storefrontDb'de Marten dokümanı olarak saklanır.
Query MCP tool'u (`search_storefront_products`) ve anonim REST endpoint'i olarak açılır; ChatAgent'ın
iki agent'ına da verilir, Catalog `search_products` public agent'tan çıkar.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten 9.5.0 + **Marten.PgVector 9.5.0** + **Pgvector 0.3.2** (yeni, CPM'e),
Wolverine 6.4.1, ModelContextProtocol.AspNetCore 1.4.0, Microsoft.Extensions.AI.OpenAI 10.7.0 (mevcut sürüm)

**Storage**: storefrontDb (Postgres, `storefrontManagement` şeması) + pgvector uzantısı
(Marten `UsePgVector()` migration'ı kurar); imaj `pgvector/pgvector:pg17` (AppHost)

**Testing**: xUnit + Shouldly, saf domain birim testleri (`tests/Storefront.Api.Tests`)

**Target Platform**: Aspire AppHost ile dağıtık lokal çalışma (mevcut)

**Project Type**: Mikroservis (mevcut Storefront.Api içinde vertical slice) + ChatAgent/AppHost dokunuşları

**Performance Goals**: Arama sohbet-etkileşimli (~saniyeler); embedding cast-scan binlerce ürün için yeterli,
HNSW bilinçli ertelendi (research R2)

**Constraints**: Embedding hatası view yazımını ENGELLEYEMEZ (FR-014); publish akışı değişmez;
oversell/rezervasyon akışlarına dokunulmaz

**Scale/Scope**: Ürün sayısı binler mertebesi; sonuç ≤ 20; tek yeni query slice + 1 yeni doküman tipi

## Constitution Check

*GATE: v1.3.1'e göre değerlendirildi — İHLAL YOK.*

- **I. BC İzolasyonu**: PASS — arama yalnız Storefront'un kendi DB/dokümanlarını okur; veri girişi mevcut
  integration event'lerden; ChatAgent erişimi MCP ile. Başka BC'nin DB'sine erişim yok.
- **II. Zengin aggregate**: PASS — yeni aggregate yok. `ProductEmbedding` read-model yan dokümanıdır
  (StorefrontView gibi), domain invariant taşımaz; aggregate olarak modellenmez.
- **III. Vertical Slice + CQRS, repository yok**: PASS — tek query slice `Features/Agent/SearchStorefrontProducts`
  (yalnız okur); handler `IDocumentSession`/bağlantısını doğrudan kullanır; MCP tool ince sarmalayıcı,
  aynı query'yi `IMessageBus` ile çağırır. Embedding üretimi mevcut event handler'ında (yazma yolu).
- **IV. Result pattern**: PASS — `FeatureListResultModel<T>`; boş=NotFound; doğrulama/servis hataları
  resource sabitli `MessageItem` ile Error.
- **V. Scope-tabanlı yetki**: PASS — arama anonim (mevcut storefront read endpoint'leri gibi `AllowAnonymous`);
  rol yok, yeni scope gerekmez.
- **Teknoloji kısıtları**: PASS — paketler CPM'e; sistem Aspire'dan çalışır; DI'da generator kaydı
  Program.cs'te açık framework çağrısıyla (kullanıcı tercihi: dolaylama yok); using'ler GlobalUsings'e.

*Post-design re-check: PASS (değişiklik yok).*

## Project Structure

### Documentation (this feature)

```text
specs/019-hybrid-product-search/
├── spec.md
├── plan.md              # bu dosya
├── research.md          # R1-R8 kararları
├── data-model.md        # ProductEmbedding + query/response modeli
├── quickstart.md        # canlı doğrulama senaryoları
├── contracts/
│   └── search-storefront-products.md
└── tasks.md             # /speckit-tasks üretecek
```

### Source Code (repository root)

```text
src/services/storefront/Storefront.Api/
├── Program.cs                          # UsePgVector, embedding generator DI (fail-fast), yeni endpoint map
├── StorefrontEventHandlers.cs          # ProductChangedEvent: view save + hash-diff embedding üretimi
├── Storefront.Api.csproj               # + Marten.PgVector, Pgvector, Microsoft.Extensions.AI.OpenAI
├── GlobalUsings.cs                     # yeni namespace'ler
└── Domains/StorefrontView/
    ├── ProductEmbedding.cs             # yeni Marten dokümanı + arama metni/hash kurucu
    ├── StorefrontMcpTools.cs           # yeni: search_storefront_products (ince sarmalayıcı)
    ├── StorefrontViewEndpointExtension.cs  # + /search endpoint (AllowAnonymous)
    └── Features/Agent/
        └── SearchStorefrontProducts.cs # query + validation + hibrit handler (LINQ / raw SQL)

src/aspire/AppHost/AppHost.cs           # WithImage("pgvector/pgvector","pg17") — WithDataVolume'dan önce

src/agents/ChatAgent/
├── ConstValues.cs                      # McpServers.Storefront + StorefrontTools
└── Program.cs                          # storefront MCP URL; allowlist değişimi (public swap, assistant ekleme)

Directory.Packages.props                # Marten.PgVector 9.5.0, Pgvector 0.3.2

tests/Storefront.Api.Tests/
├── ProductEmbeddingTests.cs            # arama metni kurma + hash değişim tespiti
└── SearchStorefrontProductsTests.cs    # parametre doğrulama, MaxResults kırpma, filtre birleşimi
```

**Structure Decision**: Mevcut vertical-slice düzeni korunur; tüm arama kodu Storefront.Api'nin
`Domains/StorefrontView` dikey diliminde, entegrasyon dokunuşları AppHost/ChatAgent'ta minimal.

## Complexity Tracking

İhlal yok — tablo boş.