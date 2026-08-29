# Implementation Plan: Personalization Signal Store (Faz 1)

**Branch**: `048-personalization-signal-store` | **Date**: 2026-08-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/048-personalization-signal-store/spec.md`

## Summary

Yeni bir .NET bounded context — **Personalization.Api** — kişiselleştirme için ham
sinyalleri kendi Postgres/Marten deposunda biriktirir (write-only). İki giriş:
(1) **gezinme** sinyalleri WebApp'ten (BFF) doğrudan HTTP POST ile, kayıp-toleranslı;
(2) **satın-alma** sinyali Order BC'nin yeni yayınlayacağı `OrderCompleted` integration
event'inden, kayıpsız. Kademe: **Tam** (yeni servis + yeni DB + yeni integration event
+ yeni endpoint kontratı). Öneri/segment/ML/serving/export bu fazın dışında.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Marten (+ Marten.Newtonsoft), WolverineFx (+ RabbitMQ, Marten,
Postgresql), Asp.Versioning.Http, Scrutor, JwtBearer; `Common`, `Shared`, `ServiceDefaults`
proje referansları. WebApp tarafı: Refit + service discovery (mevcut desen).

**Storage**: PostgreSQL (yeni `personalizationApiDb`), Marten document store; şema
`ApplyAllDatabaseChangesOnStartup` ile otomatik. Diğer BC DB'leriyle paylaşım YOK.

**Testing**: xUnit + Shouldly (saf domain birim testleri — İlke VI). Handler/endpoint/
wiring test-sonrası / canlı doğrulama.

**Target Platform**: Aspire AppHost ile ayağa kalkan Linux/konteyner servis (bağımsız
çalıştırılmaz; service discovery + conn-string enjeksiyonu).

**Project Type**: Web service (bounded context) + WebApp (BFF) entegrasyonu +
Order BC'ye publisher eklemesi.

**Performance Goals**: Gezinme sinyali toplama sayfa yanıt yoluna ölçülebilir gecikme
EKLEMEZ (SC-002): WebApp'te Enqueue O(1), gönderim arka planda batch. Satın-alma
işleme nadir + idempotent.

**Constraints**: Gezinme kayıp-toleranslı (tampon taşması + servis kesintisi = sessiz
düşüş, ana akış korunur). Satın-alma kayıpsız (durable event + retry). PII YOK.
Personalization kapalıyken alışveriş akışı bozulmaz (SC-003).

**Scale/Scope**: Demo/öğrenme ölçeği. Gezinme yüksek-hacim; satın-alma düşük-hacim.
Retention politikası bu fazda tanımlanmaz (uzun tut varsayımı; sonraki faz).

## Constitution Check

*GATE: Phase 0 öncesi ve Phase 1 sonrası re-check.*

- **İlke I — BC İzolasyonu**: ✅ Yeni `personalizationApiDb` + kendi Marten şeması;
  başka BC'nin DB/tablo/aggregate'ine erişim yok. Satın-alma = `OrderCompleted`
  **integration event** (RabbitMQ fanout; binding tüketicide). Gezinme = **WebApp (BFF,
  BC DEĞİL) → Personalization.Api HTTP** — WebApp'in mevcut servis çağrılarıyla (Order/
  Storefront) aynı sınıf; cross-BC değil. **Telemetri istisnası (v1.9.0) uyumu**: gezinme
  kayıp-toleranslı, domain-gerçeği olmayan telemetri; tek tüketici (Personalization).
  042'nin dosya-kanalı yerine doğrudan HTTP seçildi (BFF→servis normal). İkinci tüketici
  doğarsa integration event'e terfi (istisnanın ruhu korunur). Not: bu, telemetri
  istisnasının "dosya" biçimini genişletir — gerekçe research.md'de.
- **İlke II — Zengin Aggregate**: ⚠️ Gerilim + çözüm (bkz Complexity Tracking).
  `PurchaseSignal` = invariant'lı aggregate (idempotent OrderId, kalem>0, adet>0,
  tutar≥0; `Create` fabrikası `ResultDomain`). `BehaviorSignal` = **telemetri kaydı**
  (write-once, domain-gerçeği değil) → aggregate DEĞİL, doğrulayan `Create` fabrikalı
  Marten document (conventions: read-model/non-aggregate BC'de ayrı yerleşebilir; İlke I
  telemetri istisnası bunu "telemetri" sayar). Anemik-aggregate ihlali yok: davranışsız
  kavram aggregate yapılmadı.
- **İlke III — VSA + CQRS, Repository Yok**: ✅ Yazma slice'ları `Features/Commands/`
  (`[Transactional]`, `IDocumentSession`); bu faz query YOK (write-only). Endpoint
  Minimal API + `*EndpointExtension`. `OrderCompleted` tüketimi `*EventHandlers`.
  Repository yok.
- **İlke IV — Result Pattern**: ✅ Handler `FeatureResultModel`/`FeatureObjectResultModel`;
  fabrikalar `ResultDomain`; hata kodları `PersonalizationResourceConstants`.
- **İlke V — Scope Yetki**: ✅ Gezinme ingest endpoint'i **statik ingest scope**
  (`personalization.ingest`) ile korunur; WebApp bunu **client_credentials makine
  kimliğiyle** sunar (anonim son-kullanıcıda bile endpoint scope-gated; İlke V makine
  kimliği = client_credentials + statik scope). Son-kullanıcı kimliği (userId/anonymousId)
  **payload'da** taşınır, token'da değil. Satın-alma yolu event-driven (HTTP auth yok).
- **İlke VI — Domain-TDD**: ✅ `PurchaseSignal.Create` + invariant'lar, value object'ler,
  `BehaviorSignal.Create` doğrulaması test-first; tasks'ta test task'ları önce.
- **İlke VII — Domain Süreci Legibility**: ✅ Yeni BC → `src/services/personalization/
  Personalization.Api/FLOW.md` bu PR'da yazılır (süreç: sinyal girişi → kalıcılık).

**Sonuç**: İlke II gerilimi Complexity Tracking'de gerekçeli; başka ihlal yok. GEÇER.

## Project Structure

### Documentation (this feature)

```text
specs/048-personalization-signal-store/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 çıktısı
├── data-model.md        # Phase 1 çıktısı
├── quickstart.md        # Phase 1 çıktısı
├── contracts/           # Phase 1 çıktısı
│   ├── order-completed-event.md      # yeni integration event
│   ├── ingest-signals-endpoint.md    # POST /v1/signals HTTP kontratı
│   └── behavior-signal-line.md       # gezinme sinyal gövdesi şeması
└── tasks.md             # /speckit-tasks çıktısı (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/services/personalization/
├── (mevcut Python: main.py, train.py, ... — DOKUNULMAZ)
└── Personalization.Api/                      # YENİ .NET servisi (BC)
    ├── Personalization.Api.csproj
    ├── Program.cs                            # Marten + Wolverine + auth + ingest scope
    ├── GlobalUsings.cs
    ├── FLOW.md                               # İlke VII domain süreci
    ├── PersonalizationEventHandlers.cs       # OrderCompleted tüketici (IncludeType!)
    ├── Constants/
    │   └── PersonalizationResourceConstants.cs
    ├── Dependencies/DependencyExtensions.cs  # Scrutor AddAllDependencies
    ├── Options/                              # (gerekirse) tip'li config
    └── Domains/
        ├── PurchaseSignals/
        │   ├── PurchaseSignal.cs             # AggregateRoot + Create + items
        │   ├── ValueObjects/PurchaseSignalValueObjects.cs
        │   ├── PurchaseSignalEndpointExtension.cs   # (bu faz endpoint gerekmeyebilir)
        │   └── Features/Commands/RecordPurchaseSignal.cs
        └── BehaviorSignals/
            ├── BehaviorSignal.cs             # telemetri document + Create
            ├── BehaviorSignalEndpointExtension.cs    # POST /v1/signals
            └── Features/Commands/IngestBehaviorSignals.cs

src/others/Shared/
├── IntegrationEvents.cs                      # + OrderCompleted record
└── RabbitMqConstants.cs                      # + OrderCompleted exchange/queue

src/services/order/Order.Api/
├── Program.cs                                # + OrderCompleted exchange declare + publish
└── Sagas/CheckoutSaga.cs                     # MarkCompleted noktasında publish

src/aspire/AppHost/AppHost.cs                 # + personalizationApiDb + personalization-api

src/ui/WebApp/
├── Program.cs                                # + IPersonalizationRefitService + client_credentials
└── Services/Behavior/
    ├── BehaviorLogWriter.cs                  # çıkış: File.Append → HTTP POST (batch)
    └── (BehaviorEvent, AnonymousIdMiddleware — mevcut, korunur)

tests/Personalization.Api.Tests/             # YENİ — saf domain birim testleri
└── PurchaseSignalTests.cs, BehaviorSignalTests.cs, VO testleri
```

**Structure Decision**: Yeni .NET servisi `src/services/Personalization.Api/`
altına, mevcut Python trainer ile aynı BC klasöründe (ileride "polyglot tek-BC"
birleşmesine hazır; şimdilik ayrı DB). Aspire resource adı `personalization-api`
(Python `personalization` ile çakışmaz), DB `personalizationApiDb`. Reviews.Api şablon
alınır. WebApp yalnız çıkış hedefini değiştirir (dosya→HTTP), kuyruk deseni korunur.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `BehaviorSignal` zengin aggregate DEĞİL (telemetri document) | Gezinme sinyali write-once telemetri; domain invariant'ı/yaşam döngüsü yok. İlke I v1.9.0 bunu açıkça "davranış telemetrisi, domain-gerçeği değil" sayar. Anemik-aggregate yapmak İlke II'yi ihlal ederdi. | Onu AggregateRoot yapmak = davranışsız (anemik) aggregate → İlke II ihlali. VO yapmak = kimliği/kalıcılığı olan kayıt VO olamaz. Doğru yapı: doğrulayan `Create` fabrikalı Marten document (read-model/telemetri istisnası). |
| İki ayrı DB (`personalizationApiDb` + Python `personalizationDb`) | Kullanıcı kararı: .NET yazar, Python (042) dokunulmaz. Şimdi tek DB paylaşımı Python ingest'ini söker (kapsam dışı). | Tek DB paylaşımı = Python `db.py CREATE TABLE` ile şema çakışması + 042'ye dokunma. "Polyglot tek-BC" birleşmesi ML fazına bilinçli ertelendi. |
| Gezinme telemetrisi dosya yerine HTTP | Kullanıcı doğrudan istek istedi; WebApp→servis HTTP zaten mevcut norm; tek sink (birleşme). | Dosya-kanalı (042 istisnası) = ikinci paralel mekanizma + Python'a bağ. HTTP = tek sahip, non-blocking queue korunur, kayıp-toleransı client'ta. |