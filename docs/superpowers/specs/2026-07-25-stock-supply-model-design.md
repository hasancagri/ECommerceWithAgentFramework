# Tasarım: Feed-Otoriteli Stok Supply Modeli (Model C Revizyonu)

**Tarih:** 2026-07-25
**Durum:** Tasarım onaylandı — writing-plans öncesi
**Bağımlılık:** 012-stock-reservation merge edilmiş olmalı (rezervasyon/commit üstüne kurulu)

## Problem

012 (Model C) tedarikçi feed'inin stoğu ezmesini yasakladı: canlı rezervasyon/commit'i
bozup oversell üretme korkusuyla `StockWrite` ingestion aşaması kaldırıldı. Sonuç: tedarikçinin
restok/azaltması sisteme hiç yansımıyor — stok bayat kalıyor.

İstenen: feed yeniden stoğu güncellesin, AMA rezervasyonları ve satışları ezmeden.

## Çözüm Özeti (Yaklaşım 2)

`ProductStock` içinde üç kavramı ayır: **Supply** (feed otoritesi), **rezervasyonlar**
(sepet, TTL) ve **SoldInCycle** (döngü-içi satış). Availability bunlardan türetilir. Feed yalnız
Supply'ı yazar ve döngü sayacını sıfırlar → ne oversell ne çift-sayım.

## Domain Modeli

| Alan | Anlam | Kim değiştirir |
|---|---|---|
| `Supply` (bugünkü `Quantity`/`OnHand` yerine) | Feed'in mutlak sayısı | Yalnız feed (`SetSupply`) |
| `_reservations` (mevcut, TTL) | Sepetteki aktif tutmalar | Basket → `SetReservedQuantity`/`Release` |
| `SoldInCycle` (yeni, int) | Bu feed döngüsünde commit edilen adet | `Commit` artırır; `SetSupply` sıfırlar |

**Türetilen:**
```
Available = max(0, Supply − aktifRezerve − SoldInCycle)
```

**Invariant'lar:**
- `Supply ≥ 0` (feed negatif set edemez — mevcut kural korunur).
- Oversell (`Supply < aktifRezerve + SoldInCycle`) hata değil; `Available` 0'a kırpılır,
  `IsOversold` tespit edilebilir kalır (mevcut desen).

**Değişmeyen:** `_reservations` yapısı, TTL, `Release`, `PurgeExpired` (sweep) — feed'den bağımsız.

## Aggregate Davranış Değişiklikleri

Üç metot değişir; gerisi aynı.

**1) `Commit`** — Supply'a dokunmayı bırakır. Rezervasyonu tüketir + `SoldInCycle += quantity`.
Commit öncesi/sonrası `Available` değişmez (reserved birim → soldInCycle kovasına taşınır).

**2) `SetSupply`** (bugünkü `SetQuantity` yerine) — feed'in tek yazım noktası:
`Supply = quantity; SoldInCycle = 0;`. Negatif-koruma aynı. Rezervasyonlara dokunmaz.
Döngü sınırını çizen tek işlem budur.

**3) Rezervasyon tavanı — `SetReservedQuantity`:**
Tavan `Quantity − ActiveReservedByOthers` yerine `Supply − ActiveReservedByOthers − SoldInCycle`.
(SoldInCycle birimleri de supply'dan çıkmıştır.)

**Akış örneği (Supply=10):**
1. A 2 rezerve → Available 8
2. A commit 2 → reservation silinir, SoldInCycle=2 → Available `10−0−2=8` (değişmez ✓)
3. Feed=8 gelir → `SetSupply(8)`, SoldInCycle=0 → Available `8−0−0=8` (çift-sayım yok ✓)
4. Feed yine 10 gelseydi (tedarikçi bağımsız) → Available 10; 2 sold "unutulur" — kabul edilen dar yarış

## Ingestion: StockWrite'ın Geri Gelişi

Altyapı zaten var: `set_stock` MCP tool'u + `SetStock` command'i (upsert; kayıt yoksa açar).

**1) Workflow yeniden 3 aşama:** `CatalogWrite → StockWrite → DiscountWrite`.
StockWrite, `job.ProductId` ile `set_stock(productId, feed.StockQuantity)` çağırır. Her ingestion'da
(create+update) mutlak set — `CatalogAction`'a ihtiyaç yok. Hata → `job.Failure` → retry/DLQ;
absolute set → retry idempotent. Failure guard'ı (`if Failure is not null return job`) diğer
executor'lardaki gibi; `Completed`'ı yine yalnız terminal `DiscountWrite` yazar.

**2) `set_stock` semantiği:** Altındaki `SetStock`/`SetQuantity` → `SetSupply` olur (Supply set +
`SoldInCycle=0`). Tool/command adı `set_stock` kalır (churn yok). Manuel admin `set_stock`'u da
aynı semantiği alır (admin sayısı taze gerçek → sold sıfırlanır).

**3) İki-yazıcı uzlaştırması:** `ProductCreated → Stock` seed (her ürün oluşumunda) + StockWrite
(yalnız feed ürünleri). **İkisi de kalır:** feed ürününde her ikisi aynı feed değerini set eder
(upsert + idempotent → zararsız); manuel üründe yalnız seed çalışır. Minimum değişiklik.

**4) Catalog `initialStock` aynı kalır** — ProductCreated seed değerini taşır; StockWrite pekiştirir.

## Sözleşme Etkisi

- **gRPC proto DEĞİŞMEZ.** `ReserveStock/CommitStock` iç davranışı değişir (Commit → SoldInCycle++,
  tavan → Supply−others−sold); proto yüzeyi aynı. `ReservationReply.Available` türetileni taşır.
- **Read-model (Storefront):** `StockChangedEvent(ProductId, Quantity)` alanı artık `Supply`
  taşır, yalnız `SetSupply`'da yayınlanır. Storefront browse görünürlüğü = Supply. Kesin available
  (−reserved−sold) checkout-anı gRPC işi olarak kalır (bugünkü decoupling'le tutarlı).
  Genişletme (browse da Available yansıtsın) ayrı, daha büyük bir iş — bu kapsamda DEĞİL.

## Hata Yolu

- StockWrite fail → `job.Failure` → retry/DLQ (idempotent).
- Oversell → `Available` 0'a kırpılır; `IsOversold` handler'da loglanır.
- Feed negatif → reddedilir (invariant).

## Test (repo konvansiyonu: saf domain unit)

`ProductStock`:
- Commit `SoldInCycle`'ı artırır, `Supply`'a dokunmaz.
- `SetSupply` `SoldInCycle`'ı sıfırlar.
- `Available = max(0, Supply − reserved − sold)`.
- Rezervasyon tavanı `SoldInCycle`'ı sayar.
- 4-adım senaryo (Supply=10 → reserve → commit → feed refresh).

IngestionAgent: StockWrite executor failure guard'ı + `set_stock` çağrısı; `WriteDecisionTests`
güncellenir.

Canlı: feed supply'ı ezmeden günceller; rezervasyonlar korunur; oversell penceresi feed peryodu
(Hangfire ~30 dk) ile sınırlı.

## Kapsam ve Ön Koşullar

- **Tam feature** (aggregate değişimi + anayasa + servisler-arası semantik) → tam spec-kit akışı.
- **Anayasa amendment (ön koşul):** Model C ("feed stoğu ezmez") revize edilir. Bu bir revert
  değil; feed Supply'ı yazar ama Supply/SoldInCycle ayrımı 012'nin oversell korkusunu giderir.
  Repo kuralı: önce anayasa amendment, sonra kod.
- **012 merge'ine bağımlı.**

## Kapsam Dışı (YAGNI)

- Browse görünümünün Available (−reserved−sold) yansıtması.
- "Hangi feed düşüşü bizim satışımız" bulanık uzlaştırması (reconciliation) — reset-on-refresh
  yeterli.
- `CatalogAction` diriltilmesi — gerekmez (StockWrite her seferinde absolute set).