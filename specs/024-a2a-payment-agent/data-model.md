# Data Model: 024 A2A Installment Quote

Bu feature çoğunlukla **davranış** (A2A istemci + intent delege) getirir; kalıcı yeni
aggregate yoktur. Tek kalıcı değişiklik: Customer BC `SavedCard`'a **BIN** alanı.

## Değişen kalıcı model — Customer BC (Wallet)

### `SavedCard` (mevcut sade entity — Wallet aggregate içinde)

Yeni alan eklenir; PAN/CVV yine **saklanmaz** (INV-3 korunur).

| Alan | Tür | Not |
|------|-----|-----|
| `Bin` | `string` | **YENİ.** Kartın ilk 6 hanesi. Hassas değil. AddCard'da yakalanır. |
| Token | string | mevcut — opak gateway token |
| Brand | string | mevcut — ağ markası (Visa/Mastercard) |
| Last4 | string | mevcut |
| ExpiryMonth/Year | int | mevcut |
| IsDefault | bool | mevcut — default kart seçimi (SetDefaultCard) |

- `SavedCard.Create(...)` imzasına `bin` eklenir; `Bin` private set + expose.
- Doğrulama: `Bin` tam 6 haneli rakam (yoksa/eksikse boş kabul + BIN'siz sorgu fallback).

### `TokenizeResult` (Tokenization) — yeni `Bin` alanı

- `SimulatedCardTokenizer` PAN'ın ilk 6 hanesini `Bin` olarak döndürür (satır 13'te zaten
  digits var). Gerçek gateway gelince BIN oradan gelir; Wallet kodu değişmez.
- `AddCard` handler `TokenizeResult.Bin`'i `SavedCard.Create`'e taşır.

### Yeni okuma yüzeyi — default kartın BIN'i

- Assistant'ın BIN'i alması için Customer BC Agent/Query slice: default kartın `Bin`'ini
  (+ brand/last4) döndüren okuma. Mevcut `Features/Agent/GetCards` genişletilir ya da
  `GetDefaultCardBin` eklenir (MCP tool ince sarmalayıcı). PAN/token expose EDİLMEZ.

## Geçici (kalıcı olmayan) görüntü verisi — ChatAgent

Uzak A2A yanıtından türetilir, saklanmaz:

- **InstallmentQuote**: `bank`, `networkBrand`, `currency`, `options[]`.
- **InstallmentOption**: `installmentCount`, `perInstallmentAmount`, `totalAmount`,
  `commissionRate`. (Kontrat: `contracts/a2a-installment-agent.md`.)

## Girdi türevi (kalıcı değil)

- **Sepet toplamı**: Basket MCP `get_basket` yanıtından hesap/okuma (mevcut yetenek).
- **BIN**: Customer BC default karttan (yukarıdaki okuma yüzeyi).

## Migration / şema

- Marten document store — `SavedCard`, `Wallet` document'ı içinde gömülü; şema migration'ı
  yok, yeni alan eski dokümanlarda `null`/boş gelir (BIN'siz fallback ile uyumlu).
- Eski kartlarda BIN yok → o kartlar default iken BIN'siz genel sorgu yapılır (graceful).