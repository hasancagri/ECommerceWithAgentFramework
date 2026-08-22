# Implementation Plan: Ürün Yorumları ve Puanlama (Reviews)

**Branch**: `044-product-reviews` | **Date**: 2026-08-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/044-product-reviews/spec.md`

**Kademe**: TAM — yeni BC (Reviews.Api + reviewsDb), yeni integration event, yeni gRPC kontratı,
yeni agent. Anayasa "Artefakt Ölçekleme" gereği tam akış.

## Summary

Yeni izole **Reviews BC**: satın-alma şartlı yorum (1-5 yıldız + opsiyonel metin), herkese açık
sayfalı liste, ürün başına tek yorum. Satın-alma kanıtı Order BC'ye **senkron gRPC** ile sorulur
(anlık evet/hayır, fail-closed — anayasa İlke I sanksiyonlu senkron RPC). Puan özeti
`ReviewSummaryChanged` fat event'iyle Storefront read-model'ine denormalize edilir (kart + detay
yıldızı). Küfür/hakaret denetimi **ModerationAgent** (MAF ChatClientAgent, 041 EnrichmentAgent
emsali) ile async koşar: yayın hemen (fail-open), ihlalde otomatik `Hidden` + özet düşümü.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten 9.5.0 (document store), Wolverine 6.4.1 (bus + RabbitMQ),
Microsoft Agent Framework (`Microsoft.Agents.AI.*`) + `Microsoft.Extensions.AI` (OpenAI),
Grpc (Order sunucu / Reviews istemci), Scrutor, Aspire

**Storage**: Yeni Postgres DB `reviewsDb`, Marten şema `reviewsManagement` — yalnız Reviews.Api erişir

**Testing**: xUnit + Shouldly; saf domain testleri (İlke VI test-first: `Review` davranışları)

**Target Platform**: Aspire AppHost altında yeni `reviews-api` resource'u

**Project Type**: Mikroservis (mevcut çözüme yeni servis) + WebApp/Storefront dokunuşları

**Performance Goals**: Yorum yazma < 1sn (gRPC doğrulama dahil); özet yayılımı ≤ 10sn (SC-002)

**Constraints**: Yazma fail-closed (Order gRPC erişilemezse RED); denetim fail-open (yorum görünür
kalır, retry 10s/30s/60s → error queue); moderasyon kararını agent değil aggregate uygular

**Scale/Scope**: Tek aggregate (`Review`) + türetilmiş özet; 4 dokunulan proje
(Reviews.Api yeni, Order.Api gRPC sunucu, Storefront.Api projeksiyon, WebApp UI)

## Constitution Check

*GATE — tasarım sonrası yeniden değerlendirildi: GEÇTİ.*

- **İlke I (BC izolasyonu)**: Reviews kendi DB'si (`reviewsDb`) + şeması; ürün/kullanıcı opak Id.
  Kanallar sanksiyonlu: satın-alma kanıtı = senkron gRPC (anlık evet/hayır — İlke I istisna
  tanımına birebir; 012/028 emsali), özet dağıtımı = integration event (fanout). Cross-DB yok. ✅
- **İlke II (zengin aggregate)**: `Review` aggregate — `Create` fabrikası puan/metin guard'ları,
  `Hide(reason)` davranışı; anemik değil. Özet (`ProductReviewSummary`) aggregate DEĞİL, Marten
  sorgusuyla türetilir (read). ✅
- **İlke III (VSA+CQRS)**: slice'lar Commands/Queries ayrık; repository yok; Minimal API +
  EndpointExtension; agent slice yok (MCP yüzeyi açılmıyor — ihtiyaç yok, YAGNI). ✅
- **İlke IV (Result)**: `SubmitReview` guard'ları `ResultDomain`; handler'lar Feature*Model;
  hata kodları `ReviewsResourceConstants`. ✅
- **İlke V (scope)**: yazma `reviews.write` (KnownScopes'a eklenir, customer rolüne map);
  okuma anonim. Order gRPC ucu `reviews.purchase-check` DEĞİL — mevcut desene uyup kullanıcı
  bearer'ı forward edilir (BearerForwardingHandler) + `order.read` benzeri dar scope: karar
  research.md R4. ✅
- **İlke VI (Domain-TDD)**: `Review.Create` guard'ları + `Hide` geçişleri + ad maskeleme VO
  test-first; handler/endpoint/agent hariç. ✅
- **Telemetri istisnası kullanılmıyor** — Reviews domain gerçeğidir, event/gRPC ile taşınır. ✅

## Project Structure

### Documentation (this feature)

```text
specs/044-product-reviews/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── reviews-rest-api.md
│   ├── order-purchase-check-grpc.md
│   ├── review-summary-event.md
│   └── moderation-agent-output.md
└── tasks.md  (speckit-tasks üretir)
```

### Source Code (repository root)

```text
src/services/reviews/Reviews.Api/            # YENİ servis (BC)
├── Program.cs                               # Marten+Wolverine+auth+gRPC istemci+agent kayıt
├── Constants/ReviewsResourceConstants.cs
├── Options/ModerationOptions.cs             # OpenAI ApiKey+Model, fail-fast
├── Domains/Reviews/
│   ├── Review.cs                            # aggregate (ReviewStatus enum aynı dosyada)
│   ├── ReviewEndpointExtension.cs
│   ├── ValueObjects/ReviewValueObjects.cs   # ReviewerName (maskeleme), ModerationVerdict
│   └── Features/
│       ├── Commands/SubmitReview.cs         # gRPC doğrulama + kayıt + event + kuyruk
│       ├── Commands/ModerateReview.cs       # kuyruk tüketicisi: agent çağır + Hide + event
│       └── Queries/GetProductReviews.cs     # sayfalı liste + özet (anonim)
└── Infrastructure/Moderation/ModerationAgent.cs  # MAF ChatClientAgent (Singleton)

src/others/Shared/
├── IntegrationEvents.cs                     # + ReviewSummaryChanged
├── RabbitMqConstants.cs                     # + reviews exchange/queue adları
└── Protos/order_purchase.proto              # YENİ: HasConfirmedPurchase(userId, productId)

src/services/order/Order.Api/                # gRPC sunucu: OrderPurchaseGrpcService (ince)
src/services/storefront/Storefront.Api/      # StorefrontView += RatingAvg/RatingCount;
                                             # StorefrontEventHandlers += ReviewSummaryChanged
src/services/gateway/                        # /reviews route (WebApp → Reviews.Api)
src/ui/WebApp/                               # detay: yıldız+liste+form; kart: yıldız rozeti
src/aspire/AppHost/AppHost.cs                # reviewsDb + reviews-api resource
src/others/Identity.Server/                  # KnownScopes += reviews.write (+rol map seed)
tests/Reviews.Api.Tests/                     # YENİ test projesi (domain, test-first)
```

**Structure Decision**: Mevcut mikroservis düzeninin birebir kopyası; yeni BC `src/services/reviews`.
Özet ayrı aggregate DEĞİL (Complexity Tracking'e gerek yok — sapma yok).

## Complexity Tracking

Sapma yok — tablo boş.