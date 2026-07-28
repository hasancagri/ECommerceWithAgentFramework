# Data Model: Basket Reservation Anchor (017)

Phase 1 çıktısı. İki BC'nin modeli ayrı ayrı verilir (İlke I — ortak model yok).

## Basket BC (`basketDb`, Marten dokümanı)

### Basket (aggregate root — değişir)

| Alan | Tip | Not |
|------|-----|-----|
| ReservationExpiresAt | `DateTimeOffset?` | YENİ, persist. Sepet çapası (mutlak UTC). Null = çapa yok. |

Türetilmiş (persist edilmez):

- `IsExpiredAt(DateTimeOffset now)` → `Items.Count > 0 && ReservationExpiresAt <= now` (FR-010).

Davranış metotları (yeni/değişen):

- `StartReservation(DateTimeOffset expiresAt)` — çapa kurar. Yalnız çapa yokken çağrılır (handler başarıda çağırır, FR-002).
- `PurgeExpiredItems(DateTimeOffset now)` — `IsExpiredAt` ise TÜM satırları düşürür + çapayı sıfırlar (FR-008). Aksi halde no-op.
- `RemoveItem(...)` — mevcut; son satır silinince çapayı sıfırlar (FR-004). `SetItem(quantity<=0)` yolu da aynı kuraldan geçer.
- `SetItem(id, name, imageUrl, price, quantity)` — `expiresAt` PARAMETRESİ KALKAR (satır bitişi artık yok).

Invariant'lar:

- Çapa, satır varken kurulabilir/yaşar; sepet boşalınca null'lanır (aggregate içinde korunur).
- Ekleme / adet değişikliği / tekil silme mevcut çapaya DOKUNMAZ (FR-003).

### BasketItem (sade entity — değişir)

| Alan | Durum |
|------|-------|
| ReservationExpiresAt | KALDIRILIR (R4). Eski dokümanlardaki JSON alanı Newtonsoft tarafından yok sayılır; migration yok. |

Diğer alanlar (Id, Name, ImageUrl, Price, PriceByApplyDiscountRate, Quantity) değişmez.

### BasketReservationOptions (YENİ — config nesnesi, domain değil)

| Alan | Tip | Varsayılan | Kaynak |
|------|-----|-----------|--------|
| ReservationDuration | `TimeSpan` | 5 dk | `Basket:ReservationDuration` (FR-013) |

## Stock BC (`stockDb`, Marten dokümanı)

### ProductStock (aggregate root — imza değişir, alan değişmez)

- `SetReservedQuantity(Guid userId, int quantity, TimeSpan ttl, DateTimeOffset now, DateTimeOffset? expiresAt = null)`
  - `expiresAt` verilmişse: yeni rezervasyon bu mutlak bitişle doğar; mevcut rezervasyonun bitişi buna EŞİTLENİR (R2).
  - `expiresAt` null ise: bugünkü davranış (ilk oluşumda `now + ttl`, mevcutta yenileme yok) — FR-006 geri düşüş.
  - Yeterlilik kuralı (`OnHand - diğerlerinin aktifi`) değişmez.

### StockReservation (gömülü entity — davranış eklenir)

| Üye | Not |
|-----|-----|
| `SetExpiresAt(DateTimeOffset expiresAt)` | YENİ. Yalnız aggregate açık mutlak bitiş aldığında çağırır; sabit-TTL yolunda kullanılmaz. |

Alanlar (UserId, Quantity, ExpiresAt) değişmez. `PurgeExpired` / sweep / `ReservationExpired` zinciri AYNEN (FR-007).

## Kontrat yüzeyleri (özet — ayrıntı contracts/)

- gRPC `SetReservedQuantityRequest`: + `expires_at` (string, ISO-8601, boş = yok). Geriye uyumlu (FR-006).
- REST `GET /v1/basket/user` response: + `ReservationExpiresAt` (nullable) + `IsReservationExpired` (bool);
  item'lardan `ReservationExpiresAt` KALKAR (FR-009/010).
- Agent `GetBasket` response: aynı sepet düzeyi alanlar eklenir (R9).

## State geçişleri (çapa yaşam döngüsü)

```text
[çapa yok] --ilk başarılı ekleme--> [çapa = now + Duration]
[çapa var] --ekleme / adet / tekil silme--> [çapa DEĞİŞMEZ]
[çapa var] --son satır silindi / OrderCreated (doküman silinir) / PurgeExpiredItems--> [çapa yok]
[çapa var, süre geçti] --yeni ekleme--> PurgeExpiredItems → [çapa yok] → yeni çapa kurulur (FR-008)
[çapa var, süre geçti] --sweep + ReservationExpired--> satırlar tek tek düşer → boşalınca [çapa yok]
```

Not: `ReservationExpired` handler'ı satır silerken aggregate'ten geçer; son satır düşünce çapa sıfırlanır (US4).