# Phase 1 Data Model: PG Aracılı Kart Saklama

Kapsam = **mağaza (bu repo)** kalıcı modeli. Kart verisi (PAN/marka/son4/token) PG/iyzico'da yaşar,
mağazada değil; burada yalnız çapa + kullanıcı-tercihi tutulur.

## Aggregate: Wallet (ince) — `customerDb`

Kullanıcı başına tek doküman (`UserId` key). Zengin aggregate: davranış + invariant içeride.

| Alan | Tip | Not |
|------|-----|-----|
| `Id` / `UserId` | Guid | Kimlik + denetim (AggregateRoot); kullanıcı başına tembel oluşur |
| `PgUserHandle` | string? | PG kullanıcı-handle (kullanıcının kart kümesi). İlk kart eklemede PG döner; sonrakiler aynı handle. `null` = hiç kart eklenmemiş |
| `DefaultCardHandle` | string? | Varsayılan kartın opak PG kart-handle'ı. ≤1 varsayılan. `null` = varsayılan yok |

**SÖKÜLEN:** `Cards` koleksiyonu (`SavedCard` yerel entity: Token/Brand/Last4/Bin/Expiry/Label/
IsDefault) + `RemovedCard`. Kart artık aggregate state'i değil → PG/iyzico canlı kaynağı.

### Davranış (test-first — İlke VI)

| Metot | Döner | Kural (invariant) |
|-------|-------|-------------------|
| `Create(userId)` | Wallet | Tembel oluşturma |
| `SetPgUserHandle(handle)` | ResultDomain | Handle boş olamaz; zaten varsa **değişmez** (aynı kullanıcı = aynı handle — ikinci ekleme yeni handle yazmaz) |
| `SetDefaultCard(cardHandle)` | ResultDomain | `PgUserHandle` var olmalı; `DefaultCardHandle = cardHandle` (öncekini ezer → tek varsayılan) |
| `ClearDefaultIfMatches(cardHandle)` | ResultDomain | Silinen kart varsayılansa `DefaultCardHandle=null` (FR-012) |
| `MarkFirstCardDefault(cardHandle)` | ResultDomain | `DefaultCardHandle` boşsa set et (FR-001a); doluysa no-op |

> Aggregate metotları yalnız handler'dan çağrılır. Kart listesi/silme PG'ye gider (aggregate state
> değil) → onlar handler'da PG client + bu metotların birleşimi.

## Entity: AddCardSession — `customerDb`

Hosted-form callback'ini kullanıcıya bağlayan tek-kullanımlık korelasyon (R3). Aggregate DEĞİL — süreç
korelasyon kaydı (`Domains/` dışı ya da Wallet altında yardımcı doc; impl kararı).

| Alan | Tip | Not |
|------|-----|-----|
| `Id` (SessionId) | Guid | Tahmin-edilemez; `conversationId` olarak PG'ye gider |
| `UserId` | Guid | Oturumu başlatan giriş-yapmış kullanıcı |
| `Status` | enum | `Pending` / `Completed` / `Cancelled` / `Expired` |
| `CreatedAt` | DateTimeOffset | Süre-sınırı (ör. 15 dk) hesabı |

**Yaşam döngüsü:** StartAddCard → `Pending` yazılır + PG'ye conversationId gider. Callback →
conversationId ile bulunur, `UserId` çözülür, PG'den handle alınır, Wallet güncellenir, `Completed`.
Süre aşımı/iptal → `Expired`/`Cancelled`; tekrar kullanılamaz.

## Çalışma-anı izdüşümü (kalıcı DEĞİL): CardView

PG list-cards yanıtından türetilir; yalnız response DTO (Marten'da tutulmaz).

| Alan | Kaynak (PG) | Chat'e döner mi |
|------|-------------|-----------------|
| `CardHandle` | PG kart-handle (opak) | Evet — sil/varsayılan referansı (Clarify Q1) |
| `Brand` | kart markası | Evet |
| `Last4` | son 4 hane | Evet |
| `ExpiryMonth/Year` | son-kullanma | Evet |
| `Alias` | kart etiketi | Evet |
| `IsDefault` | `== Wallet.DefaultCardHandle` | Evet (mağaza hesaplar) |
| PAN / CVV | — | **ASLA** |

## Ödeme bağlamı izdüşümü: PaymentContextView (S2S — Order çeker)

Mevcut `GetPaymentContextForAgent` yanıtı; alan değişimi.

| Alan | Değişim |
|------|---------|
| ~~`VaultToken`~~ | **KALKAR** |
| `PgUserHandle` | **YENİ** (Wallet'tan) |
| `CardHandle` | **YENİ** (seçilen/varsayılan kart) |
| `Buyer*` / `MerchantId` / adres | KALIR (mevcut sandbox default'ları) |

## İlişkiler

- `Wallet (1) — (0..1) DefaultCardHandle` → PG'deki bir karta işaret (mağazada kart yok).
- `Wallet (1) — (0..*) AddCardSession` → geçici korelasyon (tamamlanınca anlamsızlaşır).
- `AddressBook` — bu feature'la ilişkisiz, değişmez.

## Silinen/değişen kalıcılık özeti

- **Silinir:** `SavedCard` yerel entity + `Wallet.Cards` + `ICardTokenizer`/`GatewayCardTokenizer`
  yerel-vault PAN yolu.
- **Eklenir:** `Wallet.PgUserHandle`, `Wallet.DefaultCardHandle`, `AddCardSession` doc.
- **Migrasyon:** yok (sandbox/demo — kullanıcılar kartı yeniden ekler).