# Research: Reviews Moderasyon Agent Taşıma

Tüm belirsizlikler tasarım tartışmasında çözüldü; bu dosya kararları ve gerekçeleri sabitler.

## Karar 1 — Kod-taşıma (in-proc-benzeri worker) vs runtime-ayrım vs kütüphane

- **Karar**: Ayrı **çalışan worker servisi** (`src/agents/Reviews.Moderation`), broker üzerinden async.
- **Rationale**: Kullanıcı MessageBroker iletişimi istedi → ayrı process. Kütüphane (in-proc) BC'nin
  bağımlılık grafiğinde agent-framework'ü transitively bırakırdı; kullanıcı fiziksel ayrımı istedi.
  Moderasyon zaten async/dayanıklı bir adım olduğundan event-driven worker doğal oturuyor.
- **Alternatifler**: (A) agents/ **kütüphanesi**, in-proc çalışır — reddedildi (runtime izolasyon yok).
  (B) senkron gRPC worker — reddedildi (moderasyon request/response değil, async fail-open).

## Karar 2 — İletişim kanalı: RabbitMQ fanout + iki event

- **Karar**: `ReviewModerationRequested` (Reviews→worker), `ReviewModerated` (worker→Reviews).
  Fanout exchange; **binding'i tüketici kurar** (007 dersi).
- **Rationale**: Anayasa İLKE I sanksiyonlu kanal = integration event. Repo deseni (ProductChanged,
  BuyBoxChanged, ReviewSummaryChanged) birebir. Sözleşme `Shared.IntegrationEvents`'te.
- **Alternatifler**: tek "isteği de sonucu da taşıyan" komut — reddedildi (yön ayrımı + tüketici binding netliği).

## Karar 3 — Submit yolunun broker-dayanıklılığı

- **Karar**: `SubmitReview` `[Transactional]` handler'ında `ReviewModerationRequested`
  `bus.PublishAsync` ile yayınlanır → Wolverine+Marten **transactional outbox** mesajı reviewsDb tx'iyle
  kalıcılaşır, commit sonrası relay edilir.
- **Rationale**: Broker down olsa submit yalnız reviewsDb'ye yazar, başarı döner; mesaj outbox'ta bekler.
  Bu davranış bugün `ReviewSummaryChanged` + `ModerateReview` için zaten böyle (044 canlı PASS) —
  yeni event aynı yoldan gider, ek altyapı yok.
- **Alternatifler**: broker'a doğrudan best-effort publish + swallow — reddedildi (kısa kesintide
  moderasyon sessizce kaybolurdu; outbox neredeyse bedava daha iyi).

## Karar 4 — Post-moderation + fail-open korunur

- **Karar**: Yorum anında Visible doğar; ihlalde sonradan Hidden. Broker/worker down → Visible kalır.
- **Rationale**: Kullanıcı "moderasyon kritik değil" + "broker sorununda uygulama devam" dedi.
  Pre-moderation fail-open'ı bozardı (worker down iken yorum hiç görünmezdi). Mevcut 044 davranışı.

## Karar 5 — Worker durumsuz, DB yok; ChatAgent emsali

- **Karar**: Worker'ın DB'si yok; tek durum kaynağı Reviews'in reviewsDb'sidir.
- **Rationale**: Moderasyon saf fonksiyon (metin+yıldız → verdict). Durum tutmaya gerek yok; DB'siz
  agent servisi anayasada ChatAgent ile emsallidir (BC olma zorunluluğu DB'li servisler içindir).

## Karar 6 — Domain-TDD kapsamı (yeni test-first birim yok)

- **Karar**: Yeni test-first domain birimi eklenmez.
- **Rationale**: `Review` aggregate + `ModerationVerdict` VO değişmiyor (zaten test-first + Reviews.Api.Tests).
  Taşınan LLM çağrısı + iki handler = altyapı (İLKE VI kapsam dışı: test-sonra/canlı doğrulama).
  Regression: mevcut Reviews.Api.Tests + canlı moderasyon smoke.

## Karar 7 — Silinecekler ve OpenAI bağımlılığının taşınması

- **Karar**: Reviews'ten `Infrastructure/Moderation/*`, `Options/ModerationOptions.cs`, `ModerateReview.cs`,
  OpenAI/Microsoft.Agents.AI paket refs silinir. `ModerationOptions` (section "OpenAI") worker'a taşınır;
  OpenAI user-secret worker'a bağlanır, Reviews'in OpenAI bağı kalkar.
- **Rationale**: FR-002 (Reviews'te sıfır agent-framework) ve amaç bu. Model config worker'ın olur.
- **Alternatifler**: ModerationOptions'ı Shared'a koymak — reddedildi (yalnız worker kullanır, orada kalsın).
