# Feature Specification: Feed-Otoriteli Stok Supply Modeli (Model C Revizyonu)

**Feature Branch**: `014-stock-supply-model`

**Created**: 2026-07-26

**Status**: Draft

**Input**: Tedarikçi feed'i stoğu rezervasyon/satışı ezmeden güncellesin; 012'de kaldırılan
StockWrite ingestion aşaması, Supply/SoldInCycle ayrımıyla geri gelsin.

## User Scenarios & Testing *(mandatory)*

Aktörler: **Supplier feed** (supply otoritesi), **müşteri** (rezervasyon/sipariş), **operatör**
(stok görünürlüğü). Bu feature 012 (rezervasyon) üstüne kurulur ve 012'nin Model C kararını revize
eder. Amaç: feed supply'ı güncelleyebilsin ama oversell/çift-sayım olmasın.

### User Story 1 - Feed supply'ı ezmeden günceller (Priority: P1)

Tedarikçi feed'i bir ürünün `Supply`'ını (feed-otoriteli adet) yazar; aktif rezervasyonlar ve
döngü-içi satışlar korunur, availability doğru türetilir.

**Why this priority**: Model C'nin "feed stoğa dokunmaz" sınırını kaldırıp bayat stoğu giderir;
feature'ın var oluş nedeni.

**Independent Test**: Bir ürünü rezerve et, feed'i tetikle; `Supply` feed değerine gelir,
rezervasyon silinmez, `Available = Supply − aktifRezerve − SoldInCycle` doğru.

**Acceptance Scenarios**:

1. **Given** rezerveli ürün, **When** feed supply yazar, **Then** rezervasyon korunur ve Available
   yeni Supply'a göre türetilir.
2. **Given** feed supply < aktif rezerve, **When** yazılır, **Then** hata değil; Available 0'a kırpılır.

### User Story 2 - Satış çift-sayılmaz (Priority: P1)

Sipariş `SoldInCycle`'ı artırır (Supply'a dokunmaz); yeni feed geldiğinde `SoldInCycle` sıfırlanır,
böylece satış hem döngü içinde düşülür hem feed yetişince çift-sayılmaz.

**Why this priority**: Feed-otoriteli supply + yerel satış birleşince çift-sayım tuzağı; feature'ın
doğruluğu buna bağlı.

**Independent Test**: Rezerve→sipariş (SoldInCycle++), sonra feed refresh; SoldInCycle sıfırlanır,
Available tutarlı (çift düşme yok).

**Acceptance Scenarios**:

1. **Given** commit sonrası SoldInCycle=2, **When** feed supply yazar, **Then** SoldInCycle=0 olur.
2. **Given** Supply=10 reserve 0 sold 2, **When** get_stock, **Then** Available=8.

### User Story 3 - StockWrite ingestion aşaması geri gelir (Priority: P2)

IngestionAgent workflow'u yeniden `Catalog → Stock → Discount`; StockWrite her ingestion'da feed
supply'ını mutlak yazar (idempotent).

**Why this priority**: Feed'in supply'ı yazması bu aşama olmadan olmaz; ama US1/US2 domain'i
kurulmadan anlamsız → P2.

**Independent Test**: Feed snapshot'ı işlenince ilgili ürünün Supply'ı feed değerine gelir.

**Acceptance Scenarios**:

1. **Given** değişmiş feed kaydı, **When** ingestion işler, **Then** StockWrite `set_stock` ile
   Supply'ı yazar; create/update ayrımı gerekmez.

### Edge Cases

- Feed supply negatif? Reddedilir (mevcut invariant).
- Feed tam satıştan sonra ama onu yansıtmadan gelirse? Dar yarış; SoldInCycle sıfırlandığından o an
  Available bir fazla görünebilir (kabul edilen taviz, feed peryodu ~30 dk ile sınırlı).
- Feed-dışı (manuel) ürün oluşumu? ProductCreated seed yolu korunur; StockWrite yalnız feed ürünleri.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `ProductStock`, feed-otoriteli `Supply` + döngü-içi `SoldInCycle` alanlarını AYRI TUTMALI.
- **FR-002**: `Available = max(0, Supply − aktifRezerve − SoldInCycle)` OLMALI.
- **FR-003**: Feed yalnız `SetSupply` ile Supply'ı yazMALI ve `SoldInCycle`'ı sıfırlaMALI.
- **FR-004**: `Commit`, `SoldInCycle`'ı artırMALI ve Supply'a DOKUNMAMALI.
- **FR-005**: Yeni rezervasyon tavanı `Supply − başkalarınınRezervesi − SoldInCycle` OLMALI.
- **FR-006**: StockWrite ingestion aşaması geri GELMELİ; her ingestion'da mutlak set (idempotent),
  create/update ayrımı gerekMEMELİ.
- **FR-007**: `StockChangedEvent` Supply değerini taşıMALI, yalnız `SetSupply`'da yayınlanMALI;
  `Commit`'in gereksiz yayını kaldırılMALI.
- **FR-008**: gRPC proto DEĞİŞMEMELİ (ReserveStock/CommitStock iç davranışı değişir, yüzey aynı).
- **FR-009**: ProductCreated → Stock seed yolu KORUNMALI (feed-dışı ürünler için).
- **FR-010**: Model C anayasa ifadesi bu ayrıma göre AMENDMENT edilMELİ (kod öncesi).

### Key Entities

- **ProductStock**: `Supply` (feed-otoriteli), `_reservations` (TTL, 012), `SoldInCycle` (yeni);
  Available bunlardan türetilir.
- **StockChangedEvent**: Storefront'a Supply'ı taşıyan read-model event'i.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Rezerveli üründe feed supply yazınca rezervasyon kaybı %0 (rezervasyon silinmez).
- **SC-002**: Commit→feed refresh döngüsünde çift-sayım = 0 (Available tutarlı).
- **SC-003**: StockWrite geri geldiğinde feed'le değişen ürünün Supply'ı feed değerine %100 eşitlenir.
- **SC-004**: gRPC proto ve mevcut 012/013 testlerinde regresyon = 0.
- **SC-005**: Oversell durumunda Available 0'a kırpılır (negatif görünmez).

## Assumptions

- **012-stock-reservation MERGE edilmiştir** (bu feature onun ProductStock modelini temel alır) — DONE.
- Alan adı `Quantity` korunur (persisted = Supply); Marten şema migration'ı ve kontrat churn'ü olmaz.
- Browse görünürlüğü Supply'ı yansıtır; kesin available checkout-anı gRPC işidir (012 decoupling).
- Detaylı uygulama rehberi: `docs/superpowers/specs|plans/2026-07-25-stock-supply-model-*` (design+plan).