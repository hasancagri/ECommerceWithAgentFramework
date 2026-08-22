# Contract: Moderasyon Integration Event'leri

Kanal: RabbitMQ **fanout** (Wolverine). Kural: **yayıncı yalnız exchange deklare eder; binding'i
TÜKETİCİ kurar** (soğuk-açılış kayıp dersi — 007). Sözleşme tipleri `Shared.IntegrationEvents`'te.

## Exchange / Queue sabitleri (`Shared.RabbitMqConstants` — yeni)

```
ReviewModerationRequested
  Exchange = "reviews.moderation-requested"
  Queues.Worker = "reviews-moderation.requested"     // worker bağlar + dinler

ReviewModerated
  Exchange = "reviews.moderated"
  Queues.Reviews = "reviews.moderated"                // Reviews bağlar + dinler
```

## Event 1 — ReviewModerationRequested

```csharp
// Shared.IntegrationEvents
public record ReviewModerationRequested(Guid ReviewId, string Text, int Rating);
```

- **Yayıncı**: Reviews.Api — `SubmitReview` handler'ında, yorum Visible yazıldıktan sonra, **metin boş
  değilse** `bus.PublishAsync` (transactional outbox → broker-dayanıklı).
- **Tüketici**: Reviews.Moderation worker — kendi kuyruğunu (`Queues.Worker`) deklare edilen exchange'e
  bağlar ve dinler.
- **PII yasağı**: yalnız ReviewId + Text + Rating. UserId/isim ASLA.

Wolverine kurulumu:
- Reviews (yayıncı): `rabbit.DeclareExchange(ReviewModerationRequested.Exchange, Fanout)` +
  `PublishMessage<ReviewModerationRequested>().ToRabbitExchange(...)`.
- Worker (tüketici): `DeclareExchange(...Exchange, e => { Fanout; e.BindQueue(Queues.Worker); })` +
  `ListenToRabbitQueue(Queues.Worker)`.

## Event 2 — ReviewModerated

```csharp
// Shared.IntegrationEvents
public record ReviewModerated(Guid ReviewId, bool Violation, string Category, string Reason);
```

- **Yayıncı**: Reviews.Moderation worker — moderasyon kararı sonrası `PublishMessage<ReviewModerated>()
  .ToRabbitExchange(...)`.
- **Tüketici**: Reviews.Api — kendi kuyruğunu (`Queues.Reviews`) bağlar + dinler; handler `Review`'i
  yükleyip `ApplyModeration` uygular, ihlalde `ReviewSummaryChanged` yayınlar.
- **Category** kapalı küme: `profanity | insult | personal_attack | none`. Şema-dışı gelirse Reviews
  handler'ı `ModerationVerdict.Create` üzerinden reddeder (savunma; normalde worker garanti eder).

Wolverine kurulumu:
- Worker (yayıncı): `DeclareExchange(ReviewModerated.Exchange, Fanout)` + `PublishMessage<...>()`.
- Reviews (tüketici): `DeclareExchange(...Exchange, e => { Fanout; e.BindQueue(Queues.Reviews); })` +
  `ListenToRabbitQueue(Queues.Reviews)`.

## Hata / dayanıklılık

- **Worker LLM hatası**: `OnException<ModerationException>()` → retry 10s/30s/60s → `MoveToErrorQueue()`
  (bu politika Reviews'ten worker'a taşınır). Tükenince yorum Visible kalır (fail-open).
- **Reviews consumer**: idempotent (`ModeratedAtUtc` no-op); bilinmeyen ReviewId sessiz no-op.
- **Broker down**: submit etkilenmez (outbox); worker/Reviews binding'leri açılışta kurulur (tüketici-binding).

## Geriye/ileri uyum

- Event'ler additive büyür (yeni alan default'lu). İlk sürümde her iki event yeni olduğundan sürüm
  gerginliği yok; eski `ModerateReview` local command SİLİNİR (dış tüketici yok, yalnız Reviews-içi idi).
