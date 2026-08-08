# Phase 0 Research: Domain Sonuç Sarmalama Standardı (ECommerce)

Kararlar PaymentGateway `014` ile ortak (aynı standart). ECommerce-özel bulgular:

## Karar 1 — Kapsam call-site tabanlı
Yalnız handler'dan çağrılan aggregate davranış/fabrika metotları. (PG research ile aynı gerekçe.)

## Karar 2 — Outcome-enum sarımı → `Ok(outcome)`
ECommerce envanterinde outcome-enum dönen handler-çağrılı metot çıkmadı; kural yine geçerli.

## Karar 3 — Getter muafiyeti
Saf sorgu/getter muaf.

## Karar 4 — Fabrika muafiyeti YOK → `Ok(...)` sarımı.

## Karar 5 — ResultDomain varlığı (ÇÖZÜLDÜ)
- **Decision**: ECommerce'de `ResultDomain` **mevcut** — `src/others/Common/Results/ResultDomain.cs:5`.
  Ayrıca `FeatureResultModel`/`FeatureObjectResultModel<T>` (`Common/Results/FeatureOutputModel.cs`).
- **Sonuç**: Yeni tip eklenmez; mevcut zarf kullanılır. PaymentGateway ile aynı API.

## Karar 6 — Void mutator'lar kapsamda (ECommerce-özel netleştirme)
- **Decision**: ECommerce aggregate mutator'larının çoğu şu an `void` (`AddItem`, `SetItem`,
  `SetStatus`, `Increase`, `Decrease`, `StartReservation`, `PurgeExpiredItems`, `Update`).
  Handler'dan çağrılan durum değiştiren `void` metot, "veri yoksa `ResultDomain`" kuralıyla
  `ResultDomain` dönecek şekilde sarılır.
- **Rationale**: PaymentGateway'de mutator konvansiyonu zaten `ResultDomain` (ör.
  `SettlementAccount.UpdateDetails`). İki repo hizalanır; invariant ihlali `Error(messages)` ile
  sinyallenir (exception yerine), Result pattern'in amacı budur.
- **Alternatives**: Void mutator'ları muaf tut — iki repo arası tutarsızlık ve "asla başarısız
  olmaz" varsayımının ileride kırılması riski. Reddedildi.
- **Not (kullanıcı onayı beklenen)**: Bu, EC iş hacminin ana kısmı. Asla başarısız olmayan saf
  setter'lar için sarım tek-tiplik amaçlı; kullanıcı isterse "yalnız başarısız olabilen mutator"a
  daraltılabilir (plan review kararı).

## Uygulama deseni
Ham/void → sarılı dönüşüm çağıran güncellemesiyle atomik:
```
// önce
basket.AddItem(item);                       // void
// sonra
var r = basket.AddItem(item);               // ResultDomain
if (!r.IsSuccess) return FeatureResultModel.Error(r.Messages);
```
`ResultDomain` API: `Ok()`, `Ok(T)`, `Error(List<MessageItem>)`, `Error(MessageItem)`.
