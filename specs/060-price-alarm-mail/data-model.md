# Data Model: Fiyat Alarmı + Mail Bildirimi (060)

## Library BC (`libraryDb`, şema `library`)

### PriceAlarm (aggregate root)

Kullanıcının bir ürünün fiyat değişimini izleme kaydı (yaşayan abonelik — FR-004). `AggregateRoot`'tan türer.

| Alan | Tip | Not |
|---|---|---|
| `Id` | Guid | AggregateRoot |
| `UserId` | Guid | Alarm sahibi (`sub` claim) |
| `Email` | string | Kuruluş anında snapshot (R3); boş olabilir |
| `ProductId` | Guid | Catalog ürün Id'si (yalnız Id referansı) |
| `ProductName` | string | Kuruluş anındaki ad — mail/iz bağlamı |
| `PriceAtCreation` | decimal | Kuruluş anındaki fiyat — bağlam/iz |

**Davranışlar** (`ResultDomain` döner; Domain-TDD kapsamı):
- `Create(userId, email, productId, productName, priceAtCreation)` — statik fabrika; userId/productId boş Guid ve fiyat ≤ 0 reddedilir.
- Tetik aggregate'i DEĞİŞTİRMEZ (alarm kapanmaz); durum/`Trigger()` yok — v1'de mutator'sız kayıt aggregate'i.

**Invariantlar:**
- Aynı kullanıcı + ürüne tek alarm (FR-002) — aggregate-üstü kural; handler mevcut kaydı sorgular, varsa yenisini yazmaz (idempotent Ok döner).
- Kaldırma = hard delete (handler `session.Delete`).

**Yaşam döngüsü:** kurulur → her fiyat değişiminde tetik event'i doğurur → kullanıcı kaldırınca silinir.

### NotificationRecord (document — aggregate DEĞİL)

Gönderim sonucunun izi (FR-007). `NotificationSent` event'inden yazılır; davranışsız, append-only.

| Alan | Tip |
|---|---|
| `Id` | Guid |
| `UserId` | Guid |
| `ProductId` | Guid |
| `Email` | string |
| `Success` | bool |
| `Detail` | string (`sent` / `no-email` / hata özeti) |
| `CreatedAtUtc` | DateTime |

## Integration event'ler (`Shared/IntegrationEvents.cs`)

- `ProductChangedEvent` — MEVCUT + additive `decimal? OldPrice = null` (yalnız fiyat değişiminde dolu).
- `PriceAlarmTriggered(Guid AlarmId, Guid UserId, string Email, Guid ProductId, string ProductName, decimal OldPrice, decimal NewPrice)` — YENİ. Mail üretimine yeter; worker ek sorgu yapmaz.
- `NotificationSent(Guid UserId, Guid ProductId, string Email, bool Success, string Detail)` — YENİ.

Ayrıntı + exchange/queue adları: `contracts/integration-events.md`.

## NotificationAgent (DB'siz) — akış içi tipler

- `MailDraft(string Subject, string BodyHtml)` — Compose structured output.
- Workflow adım mesajları (Enrich→Decide→Compose→Send→Outcome arası) worker-içi record'lar; kalıcılık yok.