# Feature Specification: Supplier Gateway Transactional Outbox

**Feature Branch**: `013-supplier-outbox`

**Created**: 2026-07-25

**Status**: Draft

**Input**: User description: "Supplier.Gateway feed çekim akışındaki dual-write'ı
(RabbitMQ publish + Postgres snapshot save ayrı, atomik değil) Transactional Outbox
ile gider. Kapsam yalnızca yayıncı taraf."

## User Scenarios & Testing *(mandatory)*

Aktörler: **Supplier.Gateway** (yayıncı sınır bileşeni), **downstream tüketici**
(IngestionAgent), **operatör** (feed sağlığını izleyen). Bu bir güvenilirlik
feature'ı: kullanıcı değeri "kayıp/mükerrer yayın olmadan tutarlı feed akışı".

### User Story 1 - Atomik snapshot + yayın (Priority: P1)

Feed çekiminde bir kaydın kanonik event'i ile snapshot güncellemesi tek bir
başarı/başarısızlık birimidir: ya ikisi de kalıcı olur ya hiçbiri.

**Why this priority**: Dual-write'ın çekirdek sorunu bu. Atomiklik olmadan çökme
penceresi snapshot ile yayını ayrıştırır; feature'ın var oluş nedeni budur.

**Independent Test**: Bir kaydı işlerken save ile yayın arası çökme simüle edilir;
yeniden başlatmada snapshot ile yayınlanmış mesaj tutarlı olmalı (ikisi de ya da
hiçbiri), yarı-uygulanmış durum kalmamalı.

**Acceptance Scenarios**:

1. **Given** değişmiş bir feed kaydı, **When** çekim kaydı işler, **Then** kanonik
   event ile snapshot ilerlemesi aynı transaction'da commit olur.
2. **Given** commit anında süreç çökmesi, **When** süreç yeniden başlar, **Then**
   ne yarım snapshot ne asılı yayın kalır; kayıt sonraki çekimde temiz işlenir.

---

### User Story 2 - Broker'a güvenilir teslim (Priority: P1)

Commit edilen bir event, RabbitMQ geçici erişilemez olsa bile eninde sonunda
broker'a iletilir; teslim yayıncı sürecinden bağımsız garanti altındadır.

**Why this priority**: Atomik commit tek başına yetmez; commit sonrası mesajın
broker'a ulaşması da garanti edilmeli, yoksa "commit oldu ama gitmedi" oluşur.

**Independent Test**: Commit anında RabbitMQ kapalıyken kayıt işlenir; broker geri
gelince mesajın kendiliğinden teslim edildiği downstream'de doğrulanır.

**Acceptance Scenarios**:

1. **Given** commit edilmiş ama henüz iletilmemiş event, **When** broker erişilebilir
   olur, **Then** event operatör müdahalesi olmadan teslim edilir.
2. **Given** teslim sırasında geçici hata, **When** yeniden denenir, **Then** event
   en az bir kez teslim edilir (at-least-once).

---

### User Story 3 - Downstream idempotency korunur (Priority: P2)

Nadir çift-teslim durumunda downstream davranışı bugünküyle aynı kalır: tekrar
teslim zararsızdır, ekstra yan etki üretmez.

**Why this priority**: At-least-once teslim çift-teslim üretebilir; mevcut idempotent
tüketici sözleşmesinin bozulmadığını doğrulamak feature'ın güvenli olmasının şartı.

**Independent Test**: Aynı kanonik event iki kez teslim edilir; downstream sonucu tek
teslimle aynı olmalı (ek ürün/indirim mutasyonu yok).

**Acceptance Scenarios**:

1. **Given** aynı event'in iki teslimi, **When** downstream işler, **Then** nihai
   durum tek teslimdekiyle aynıdır.

---

### Edge Cases

- Feed'de çok sayıda değişmiş kayıt varken tek çekimde ne olur? Her kayıt kendi
  atomik commit'iyle işlenir; kısmi ilerleme (bir kısmı commit) tutarlı kalır.
- Broker uzun süre kapalıysa? Commit edilmiş event'ler kalıcı bekler, süreç yeniden
  başlasa da kaybolmaz; broker gelince iletilir.
- Değişmemiş kayıt (snapshot aynı)? Bugünkü gibi atlanır; ne yayın ne yazım olur.
- Boş/çözülemeyen feed? Bugünkü davranış korunur: mesajsız, yazımsız kapanış.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, bir feed kaydının kanonik event yayınını ve snapshot
  güncellemesini tek bir atomik işlem olarak commit ETMELİ (ya ikisi ya hiçbiri).
- **FR-002**: Commit sonrası sistem, event'in broker'a en az bir kez teslimini,
  yayıncı sürecinin sürekliliğinden bağımsız GARANTİ ETMELİ.
- **FR-003**: Sistem, commit edilmiş ama iletilmemiş event'leri kalıcı TUTMALI;
  süreç yeniden başlasa bile kaybetmemeli.
- **FR-004**: Sistem, broker geçici erişilemezken commit edilen event'i, broker geri
  geldiğinde otomatik İLETMELİ (operatör müdahalesi olmadan).
- **FR-005**: Feature, downstream idempotency sözleşmesini KORUMALI; nadir çift-teslim
  bugünkü gibi zararsız kalmalı.
- **FR-006**: Feature, yalnızca Supplier.Gateway'i (yayıncı taraf) ETKİLEMELİ;
  IngestionAgent (tüketici) değişmeden kalmalı.
- **FR-007**: Kalıcılık altyapısı, Supplier.Gateway'in kendi veritabanı sınırında
  KALMALI; başka servisin verisine erişmemeli (bounded-context izolasyonu).
- **FR-008**: Boş/çözülemeyen/değişmemiş feed davranışı DEĞİŞMEMELİ (mesajsız,
  yazımsız kapanış; değişmemiş kayıt atlanır).
- **FR-009**: Değişiklik sonrası "önce publish sonra save" sıra bağımlılığı ve onun
  ürettiği çökme-penceresi yeniden-yayını ORTADAN KALKMALI.

### Key Entities *(include if feature involves data)*

- **Kanonik feed event'i**: Bir feed kaydının downstream'e yayılan tel-bağımsız
  temsili; snapshot ile birlikte atomik commit'e girer.
- **Feed snapshot'ı**: Bir dış kimlik için en son absorbe edilmiş feed durumu; diff
  temeli. Event yayınıyla aynı transaction'da ilerler.
- **Bekleyen giden mesaj (outbox kaydı)**: Commit edilmiş ama henüz broker'a
  iletilmemiş event; kalıcı, teslim edilince temizlenir. Gateway DB sınırında yaşar.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Save ile yayın arasındaki çökme penceresinde tutarsız sonuç oranı
  %0'dır: her senaryoda ya ikisi de ya hiçbiri kalıcıdır (yarı durum yok).
- **SC-002**: Broker commit anında kapalıyken işlenen event'lerin %100'ü, broker en
  fazla bir kez geri geldiğinde operatör müdahalesi olmadan teslim edilir.
- **SC-003**: Değişmemiş bir feed'in art arda iki çekiminde yayınlanan mesaj sayısı
  0'dır (gereksiz yeniden-yayın elenmiştir).
- **SC-004**: Downstream'e çift-teslim edilen bir event, tek teslimle birebir aynı
  nihai durumu üretir (ölçülen ek mutasyon = 0).
- **SC-005**: IngestionAgent kodu ve sözleşmesi değişmeden bu feature canlı çalışır
  (tüketici tarafında 0 değişiklik).

## Assumptions

- Downstream tüketici (IngestionAgent) zaten idempotenttir ve retry/DLQ ile korunur;
  bu feature o sözleşmeye dayanır, onu değiştirmez.
- Dev ortamı tek-düğüm çalışır; kalıcı giden mesajların iletim ajanı bu modda aktiftir.
- Feed çekimi arka plan batch işidir; kayıt başına ek kalıcılık maliyeti kabul edilebilir.
- Kalıcı giden mesaj deposu, Supplier.Gateway'in mevcut veritabanında ayrı bir alanda
  tutulur; başka context'in şemasına dokunmaz.
- Mevcut fanout exchange ve kanonik event sözleşmesi olduğu gibi kalır.