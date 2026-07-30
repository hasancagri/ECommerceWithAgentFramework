# Data Model: Wallet & AddressBook (Customer BC)

Şema: `customerManagement` (Postgres `customerDb`). İki Marten document tipi (aggregate root),
her ikisi `UserId` üzerinde index. Marten Newtonsoft + non-public setter/ctor (proje deseni).

## Aggregate: Wallet (root)

Bir kullanıcının cüzdanı. `UserId`-keyli; SavedCard koleksiyonu + "≤1 varsayılan" invariant'ı.

| Alan | Tip | Not |
|------|-----|-----|
| `Id` | Guid | `AggregateRoot`'tan (denetim/soft-delete hazır) |
| `UserId` | Guid | Zorunlu (sahipsiz olamaz); Marten index |
| `_cards` → `Cards` | `List<SavedCard>` → `IReadOnlyList<SavedCard>` | private; okuma expose |

**Fabrika/Metotlar** (mutasyon yalnız buradan):

- `static Wallet Create(Guid userId)`
- `FeatureResultModel AddCard(SavedCard card)` — koleksiyona ekler; ilk kartsa isteğe göre
  varsayılan yapılabilir (spec: otomatik terfi yok → IsDefault yalnız açık `SetDefault` ile).
- `FeatureResultModel RemoveCard(Guid cardId)` — yoksa `NotFound`. Handler kartı çıkarıp
  `Store` ettikten SONRA `ICardTokenizer.RevokeAsync(token)` best-effort çağırır (fail-open;
  gateway hatası silmeyi bozmaz). Kart update yok = güncelleme de sil+ekle (eski token revoke).
- `FeatureResultModel SetDefaultCard(Guid cardId)` — hedef yoksa `NotFound`; varsa diğerleri
  `IsDefault=false`, hedef `true` (≤1 varsayılan invariant).

### Entity: SavedCard (Wallet içinde, sade entity — base almaz)

| Alan | Tip | Not |
|------|-----|-----|
| `Id` | Guid | Kimlik var, bağımsız yaşamaz |
| `Token` | string | Gateway opak token; DIŞA GÖSTERİLMEZ |
| `Brand` | string | ör. Visa/Mastercard |
| `Last4` | string | Son 4 hane |
| `ExpiryMonth` | int | 1–12 |
| `ExpiryYear` | int | 4 hane |
| `Label` | string? | Kullanıcı etiketi (ör. "iş kartım") |
| `IsDefault` | bool | Cüzdanda en fazla biri true |

- **Ham PAN/CVV YOK.** Tokenize sonrası yalnız yukarıdaki alanlar. `private` ctor + statik
  `Create(token, brand, last4, expMonth, expYear, label)`; `SetDefault(bool)` davranış metodu.

## Aggregate: AddressBook (root)

Bir kullanıcının adres defteri. `UserId`-keyli; SavedAddress koleksiyonu + "≤1 varsayılan".

| Alan | Tip | Not |
|------|-----|-----|
| `Id` | Guid | `AggregateRoot`'tan |
| `UserId` | Guid | Zorunlu; Marten index |
| `_addresses` → `Addresses` | `List<SavedAddress>` → `IReadOnlyList<SavedAddress>` | private |

**Fabrika/Metotlar**:

- `static AddressBook Create(Guid userId)`
- `FeatureResultModel AddAddress(SavedAddress address)` — boş/eksik alan doğrulaması entity
  `Create`/Address VO'da; aggregate ekler.
- `FeatureResultModel UpdateAddress(Guid addressId, Address newValue)` — yoksa `NotFound`.
- `FeatureResultModel RemoveAddress(Guid addressId)` — yoksa `NotFound`.
- `FeatureResultModel SetDefaultAddress(Guid addressId)` — hedef yoksa `NotFound`; diğerleri
  false, hedef true (≤1 varsayılan).

### Entity: SavedAddress (AddressBook içinde, sade entity)

| Alan | Tip | Not |
|------|-----|-----|
| `Id` | Guid | Kimlik var |
| `Value` | `Address` (VO) | Province, District, Street, ZipCode, Line |
| `IsDefault` | bool | Defterde en fazla biri true |

- `private` ctor + statik `Create(Address value)`; `SetDefault(bool)`, `Update(Address)`.

### Value Object: Address (Customer BC'ye özel)

`record` + private ctor + statik `Create`. Order BC'nin `Address`'inden **ayrı tip** (BC
izolasyonu — sızdırma yok).

| Alan | Tip |
|------|-----|
| `Province` | string |
| `District` | string |
| `Street` | string |
| `ZipCode` | string |
| `Line` | string |

- `static ResultDomain<Address> Create(...)` — zorunlu alanlar boşsa `Error` (FR-002).

## Invariant'lar (aggregate içinde korunur)

- **INV-1**: Wallet'ta `Cards.Count(c => c.IsDefault) <= 1` her zaman.
- **INV-2**: AddressBook'ta `Addresses.Count(a => a.IsDefault) <= 1` her zaman.
- **INV-3**: SavedCard hiçbir zaman ham PAN/CVV alanı içermez (tip düzeyinde yok).
- **INV-4**: `UserId` zorunlu; Create dışında değişmez.
- **INV-5**: ExpiryYear/Month geçmişse kart eklenemez (doğrulama AddCard yolunda; bkz. FR-009).

## Doğrulama kuralları (kaynak → yer)

| Kural | Kaynak | Yer |
|-------|--------|-----|
| Adres zorunlu alan | FR-002 | `Address.Create` → Error |
| Geçmiş son-kullanma kart reddi | FR-009 | AddCard handler / aggregate — Error |
| Tokenize başarısız → kayıt yok | FR-013 | AddCard handler — fail-closed, Store yok |
| Yalnız kendi UserId | FR-015 | Query/Command `UserId == CurrentUser.Id` filtresi |
| ≤1 varsayılan | FR-005/012 | `SetDefault*` aggregate metodu |

## Persistence notları

- `Program.cs`: `opts.Schema.For<Wallet>().Index(x => x.UserId);` +
  `opts.Schema.For<AddressBook>().Index(x => x.UserId);`
- Yazma handler'ları `[Transactional]`; `session.Store(aggregate)`.
- Soft-delete `AggregateRoot`'tan hazır; silme kayıt-satırı düzeyinde koleksiyondan çıkarma.