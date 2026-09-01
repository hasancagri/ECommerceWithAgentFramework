# Implementation Plan: Kişisel Ana Sayfa (Sipariş-Temelli Heuristik Feed)

**Branch**: `054-personal-home-feed` | **Date**: 2026-09-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/054-personal-home-feed/spec.md`

## Summary

Ana sayfa genel vitrin olmaktan çıkar: "öne çıkan kitaplar", "Tüm Kitaplara Göz At" ve navbar
"Tüm Kitaplar" girişi kalkar; yerine yalnız kullanıcıya özel liste gelir. Storefront, Order'ın
mevcut `OrderCompleted` fanout event'ine ikinci tüketici olur ve kullanıcı başına satın alınan
ürün kümesini (`UserPurchaseProfile` dokümanı) biriktirir. Yeni query slice'ı profildeki ürünlerin
kategori+yazarlarını çıkarıp o kümeden, kullanıcının almadığı (varyant ailesi dahil elenmiş)
kitapları mevcut aile-gruplama kurallarıyla döner. Sinyalsiz kullanıcı boş durum + kategori
kartları görür. ML/Python yok; tamamen .NET içi heuristik.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Marten (Postgres document store), Wolverine (in-proc `IMessageBus` +
RabbitMQ fanout), ASP.NET Minimal API, Refit (WebApp BFF istemcisi), Razor Pages (WebApp)

**Storage**: storefrontDb (Postgres/Marten) — yeni `UserPurchaseProfile` dokümanı; mevcut
`StorefrontView` okunur, değişmez

**Testing**: xUnit + Shouldly; saf domain birimi yok denecek kadar az (read-model + handler
ağırlıklı) — feed seçim/sıralama mantığı test edilebilir saf yardımcıya çekilirse test-first
(İlke VI kapsam değerlendirmesi research.md'de)

**Target Platform**: Aspire AppHost altında koşan mevcut servisler (Storefront.Api, WebApp)

**Project Type**: Mevcut mikroservis çözümüne feature ekleme (yeni servis YOK)

**Performance Goals**: Ana sayfa feed sorgusu tek kullanıcı için < ~200ms hedef (12 kartlık tek
sayfa, sayfalama yok); event tüketimi sipariş onayından ≤1 dk içinde profile yansır (SC-002)

**Constraints**: Storefront push-only kalır (dışarı senkron çağrı YOK); mevcut liste endpoint'i ve
kategori gezinme regresyonsuz; fallback ürün listesi yok; backfill yok

**Scale/Scope**: 2 servis dokunuşu (Storefront.Api + WebApp) + paylaşılan kontrat değişikliği YOK
(mevcut `OrderCompleted` aynen kullanılır); ~1 yeni doküman, 1 yeni event handler, 1 yeni query
slice + endpoint, WebApp ana sayfa + navbar değişikliği

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **İlke I (BC izolasyonu)**: UYUMLU. Storefront, Order verisine DB'den değil mevcut
  `OrderCompleted` integration event'inden (RabbitMQ fanout, ikinci tüketici) ulaşır; kontrat
  `Shared.IntegrationEvents`'te zaten var, değişmez. Senkron BC-arası çağrı eklenmez; Storefront
  push-only duruşunu korur. WebApp (BFF, BC değil) Storefront'a mevcut REST kanalından gider.
- **İlke II (zengin aggregate)**: UYUMLU — sapma yok ama not: `UserPurchaseProfile` bir read-model
  birikimidir (Storefront'un mevcut `StorefrontView` emsali gibi), zengin aggregate DEĞİLDİR;
  Storefront BC'si domain davranışı değil projeksiyon taşır. Anemik-aggregate yasağı read-model
  dokümanlarına uygulanmaz (003'ten beri yerleşik emsal).
- **İlke III (VSA + CQRS)**: UYUMLU. Yeni kişisel feed = `Features/Queries/` altında ayrı query
  slice; event tüketimi ayrı handler; repository yok, Marten `IDocumentSession`/`IQuerySession`
  doğrudan.
- **İlke IV (Result pattern)**: UYUMLU. Query handler `FeatureObjectResultModel<T>` /
  `FeatureListResultModel<T>` döner; hata kodları `Storefront/Constants`'a eklenir.
- **İlke V (scope yetki)**: UYUMLU. Kişisel feed endpoint'i kimlik ister (kullanıcıya bağlı okuma);
  mevcut Storefront read scope'u / auth düzeni research.md'de netleşir. Anonim vitrin gezinme
  (kategori listeleri) anonim kalır.
- **İlke VI (Domain-TDD)**: KOŞULLU UYUMLU. Saf domain birimi doğarsa (feed seçim/sıralama saf
  fonksiyonu) test-first yazılır; yalnız handler+Marten sorgusu kalırsa kapsam dışı (test-sonra /
  canlı doğrulama). Karar research.md'de.
- **İlke VII (FLOW.md)**: TETİKLENİR. Storefront domain süreci değişiyor (yeni tüketilen olay +
  yeni birikim adımı) — `src/services/storefront/.../FLOW.md` aynı PR'da güncellenir.

Gate: GEÇTİ (ihlal yok; Complexity Tracking boş).

*Post-design re-check (Phase 1 sonrası)*: GEÇTİ — R1–R9 kararları gate'i değiştirmedi. Not:
İlke VI kararı netleşti (saf feed sıralayıcısı test-first, R7); İlke V için `customer` rolünün
`storefront.read` scope'u canlı doğrulamada kontrol edilir (R6, kod değil DB map).

## Project Structure

### Documentation (this feature)

```text
specs/054-personal-home-feed/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 çıktısı
├── data-model.md        # Phase 1 çıktısı
├── quickstart.md        # Phase 1 çıktısı
├── contracts/           # Phase 1 çıktısı (kişisel feed endpoint kontratı)
└── tasks.md             # /speckit-tasks çıktısı (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/storefront/Storefront.Api/
├── Domains/StorefrontView/
│   ├── Features/Queries/GetPersonalFeed.cs        # YENİ: kişisel feed query slice + endpoint
│   │                                              #   (saf seçim/sıralama yardımcısı slice içinde)
│   └── StorefrontViewEndpointExtension.cs         # feed endpoint map eklenir
├── Domains/UserPurchase/                          # YENİ: read-model birikimi (aggregate değil)
│   └── UserPurchase.cs                            # doküman: {userId}:{productId} kompozit anahtar
│                                                  #   (Reviews `PurchasedProduct` emsali)
├── StorefrontEventHandlers.cs                     # Handle(OrderCompleted) overload eklenir
│                                                  #   (sınıf zaten Discovery.IncludeType'lı)
├── Program.cs                                     # order.completed → storefront.events BindQueue
│                                                  #   + Schema.For<UserPurchase>()
├── Constants/StorefrontResourceConstants.cs       # yeni hata kodları (gerekirse)
└── FLOW.md                                        # İLKE VII güncellemesi

src/ui/WebApp/
├── Pages/Index.cshtml(.cs)                        # kişisel feed + boş durum; featured/`tümü` kalkar
├── Pages/Shared/_Layout.cshtml                    # navbar "Tüm Kitaplar" girişi kalkar
├── Services/StorefrontService.cs                  # GetPersonalFeedAsync eklenir
└── Services/Refit/IStorefrontRefitService.cs      # personal-feed HTTP bağlama

tests/Storefront.Api.Tests/                        # feed seçim/sıralama saf birimi doğarsa buraya
```

**Structure Decision**: Yeni servis/proje açılmaz. Storefront.Api içinde `UserPurchaseProfile`
ayrı domain klasörü (read-model; `StorefrontView` emsalindeki gibi aggregate-dışı yerleşim
istisnası). Kişisel feed query'si mevcut `StorefrontView` domain'inin `Features/Queries/`
slice'ıdır çünkü döndürdüğü şey StorefrontView kartlarıdır. WebApp değişikliği Razor Pages
içinde kalır; yeni sayfa açılmaz.

## Complexity Tracking

> Constitution Check ihlali yok — bölüm boş.