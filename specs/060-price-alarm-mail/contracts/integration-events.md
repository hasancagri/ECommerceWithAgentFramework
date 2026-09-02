# Kontrat: Integration Event'ler (060)

Hepsi `Shared/IntegrationEvents.cs`'te; exchange/queue adları `Shared/RabbitMqConstants.cs`'e eklenir.
Fanout deseni: yayıncı exchange deklare eder, **binding'i tüketici kurar**.

## 1. `ProductChangedEvent` — DEĞİŞİKLİK (additive)

```csharp
// mevcut alanlara ek:
decimal? OldPrice = null   // yalnız fiyat değiştiğinde dolu; default null → eski tüketici kırılmaz
```

- Dolduran: `Catalog.Api` `UpdateProduct` handler (fiyat farklıysa `oldPrice`).
- Storefront etkilenmez (alanı yok sayar).
- Yeni tüketici: `Library.Api` — mevcut `product.changed` exchange'ine kendi kuyruğunu bağlar.

| | Ad |
|---|---|
| Exchange (mevcut) | `product.changed` |
| Yeni queue | `library.events` |

## 2. `PriceAlarmTriggered` — YENİ

```csharp
public record PriceAlarmTriggered(
    Guid AlarmId, Guid UserId, string Email,
    Guid ProductId, string ProductName,
    decimal OldPrice, decimal NewPrice);
```

- Yayıncı: `Library.Api` (`OldPrice != NewPrice` ise üründeki HER alarm için bir event; alarm açık kalır — FR-004).
- Tüketici: `NotificationAgent`.
- Mail'e yetecek her alan event'te; worker başka servise sormaz.

| | Ad |
|---|---|
| Exchange | `library.price-alarm-triggered` |
| Queue (worker) | `notifications.price-alarm-triggered` |

## 3. `NotificationSent` — YENİ

```csharp
public record NotificationSent(
    Guid UserId, Guid ProductId, string Email,
    bool Success, string Detail);
```

- Yayıncı: `NotificationAgent` (handler cascade-return; Outcome adımı).
- Tüketici: `Library.Api` → `NotificationRecord` dokümanı (FR-007 kalıcı iz).
- `Detail`: `"sent"` | `"no-email"` | kısa hata özeti.

| | Ad |
|---|---|
| Exchange | `notifications.sent` |
| Queue (library) | `library.notifications-sent` |

## Hata politikası (worker — kullanıcı kararı 2026-09-02)

- Mail gönderim hatası → `NotificationException` FIRLATILIR → `OnException<NotificationException>().RetryWithCooldown(10s, 30s, 60s).Then.MoveToErrorQueue()` (Reviews.Moderation deseni, FR-008).
- Compose LLM hatası exception değil: yedek şablonla gönderime devam (spec Assumption).
- At-least-once: retry nadir durumda çift mail üretebilir — kabul (spec edge case).