# Feature Specification: Hangfire Feed Scheduler

**Feature Branch**: `008-hangfire-feed-scheduler`

**Created**: 2026-07-24

**Status**: Draft

**Input**: User description: "Supplier.Gateway feed zamanlayıcısını Hangfire'a taşı (öğrenme amaçlı).
FeedScheduler silinir; RecurringJob + await'li RunAsync + sınırlı retry + delayed ilk çekim;
storage supplierGatewayDb'de ayrı hangfire şeması; dashboard yalnız dev; POST /v1/feeds/pull değişmez."

**Kademe**: Küçük — tek servis, domain modeli/kontrat/event değişmez; `hangfire` şeması kütüphane-içi
altyapı deposudur, domain tablosu/şeması değildir (anayasa "yeni tablo/şema" ölçütünün amacı domain'dir).
Yalnız `spec.md` + `tasks.md` üretilir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kalıcı zamanlanmış feed çekimi (Priority: P1)

Operatör olarak feed çekimlerinin süreç-içi uçucu bir timer'la değil, kalıcı bir zamanlayıcıyla
periyodik koşmasını istiyorum; uygulama yeniden başlasa da zamanlama tanımı korunmalı.

**Why this priority**: Zamanlanmış çekim hattın kalbidir; Hangfire'a geçişin ana gövdesi budur.

**Independent Test**: Sistem açılır; ilk çekim yapılandırılan gecikmede, sonrakiler yapılandırılan
aralıkta koşar; yeniden başlatma sonrası zamanlama tanımı yeniden oluşturulmadan sürer.

**Acceptance Scenarios**:

1. **Given** sistem yeni açıldı, **When** `FirstPullDelaySeconds` dolar, **Then** bir feed çekimi koşar.
2. **Given** sistem açık, **When** `Feeds:PullCron` zamanı gelir, **Then** periyodik çekim koşar.
3. **Given** uygulama yeniden başladı, **When** storage'a bakılır, **Then** "feed-pull" job tanımı durur.

---

### User Story 2 - Pano: izleme ve elle tetik (Priority: P2)

Operatör olarak çekimlerin geçmişini, süresini ve hatasını bir panodan izlemek; gerektiğinde
çekimi panodan elle tetiklemek istiyorum.

**Why this priority**: Öğrenme hedefinin ve "elle tetik" isteğinin görünür karşılığı panodur.

**Independent Test**: Dev ortamında `/hangfire` açılır; son koşular süre/sonuçla listelenir;
"Trigger now" ile tetiklenen çekim koşar ve geçmişte görünür.

**Acceptance Scenarios**:

1. **Given** dev ortamı, **When** `/hangfire` açılır, **Then** pano gelir ve koşu geçmişi görünür.
2. **Given** pano açık, **When** "feed-pull" elle tetiklenir, **Then** çekim koşar ve sonuç panoya düşer.
3. **Given** Development dışı ortam, **When** `/hangfire` istenir, **Then** pano map'li değildir (404).

---

### User Story 3 - Başarısız çekimde sınırlı otomatik telafi (Priority: P3)

Operatör olarak geçici feed hatalarının (ağ, 5xx) elle müdahale gerekmeden sınırlı sayıda
yeniden denemeyle telafi edilmesini istiyorum.

**Why this priority**: Değerli ama ikincil; bugünkü davranışta karşılığı yok (yalnız log).

**Independent Test**: Feed geçici hata verirken çekim tetiklenir; job başarısız görünür ve
en fazla 2 kez yeniden denenir; kalıcı hatada 2 denemeden sonra durur.

**Acceptance Scenarios**:

1. **Given** feed erişilemez, **When** çekim exception fırlatır, **Then** job failed olur ve retry planlanır.
2. **Given** hata sürüyor, **When** 2 retry da başarısız olur, **Then** job failed kalır, yeni retry açılmaz.

---

### Edge Cases

- Kilit doluyken (süren çekim) zamanlanmış/elle tetik gelirse: çekim yapılmaz, job "skipped"
  loglar ve başarılı biter — çift çekim/çift yayın olmaz.
- Pano tetiği ile `POST /v1/feeds/pull` aynı anda gelirse: aynı kilit kapısı; biri koşar, diğeri atlanır/409.
- `Feeds:PullCron` değişirse: açılışta zamanlama tanımı idempotent güncellenir (eski aralık kalmaz).
- `Feeds:PullCron` geçersizse: uygulama açılışta görünür hata verir (sessiz yanlış zamanlama olmaz).
- Feed boş dönerse: bugünkü davranış korunur — çekim mesajsız, hatasız kapanır; job başarılı görünür.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Süreç-içi periyodik timer (`FeedScheduler`) kaldırılır; periyodik çekim Hangfire
  RecurringJob `"feed-pull"` olur.
- **FR-002**: Periyot `Feeds:PullCron` config'inden doğrudan cron ifadesi olarak okunur (varsayılan
  `*/30 * * * *`); tanım açılışta `AddOrUpdate` ile idempotent senkronlanır. `PullIntervalMinutes` kalkar.
- **FR-003**: Açılıştan `Feeds:FirstPullDelaySeconds` sonra ilk çekim gecikmeli (delayed) job ile koşar.
- **FR-004**: Job, çekimi bekleyen (await) bir yol kullanır: `FeedPullService.RunAsync` — gerçek süre,
  başarı/hata job sonucuna yansır. Mevcut fire-and-forget `TryStartAsync` endpoint için aynen kalır.
- **FR-005**: Tek-çekim kilidi (aynı `SemaphoreSlim`) tüm kapılar için korunur; kilit doluysa job
  çekim yapmadan "skipped" loglar ve başarılı biter.
- **FR-006**: Çekim exception'ı job'ı başarısız kılar; otomatik retry en fazla 2 deneme ile sınırlıdır.
- **FR-007**: Job/zamanlama durumu kalıcıdır: storage `supplierGatewayDb` içinde, Marten şemasından
  ayrı `hangfire` şemasında tutulur; bağlantı Aspire'ın enjekte ettiği conn-string'dir.
- **FR-008**: Pano `/hangfire` altında yalnız Development ortamında map'lenir; diğer ortamlarda yoktur.
- **FR-009**: `POST /v1/feeds/pull` sözleşmesi ve davranışı değişmez (202 started / 409 in-progress).
- **FR-010**: Hangfire paket sürümleri `Directory.Packages.props`'a eklenir (CPM).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Açılıştan sonra ilk çekim yapılandırılan gecikme ±30 sn içinde koşar; sonraki çekimler
  yapılandırılan aralıkta düzenli koşar.
- **SC-002**: Operatör panodan son çekimlerin sonucunu ve süresini görür; elle tetiklenen çekim
  10 sn içinde başlar.
- **SC-003**: Hangi kapıdan gelirse gelsin aynı anda ikinci çekim başlamaz; mükerrer yayın gözlenmez.
- **SC-004**: Geçici feed hatası operatör müdahalesi olmadan en fazla 2 yeniden denemeyle telafi edilir.
- **SC-005**: Uygulama yeniden başlatıldığında zamanlama tanımı ve koşu geçmişi kaybolmaz.

## Assumptions

- Amaç öğrenme + kalıcı zamanlama/pano; kaçan tick'in geriye dönük telafisi (misfire catch-up) hedef değil.
- Pano dev'de auth'suz kabul edilir; Supplier.Gateway yalnız Aspire dev topolojisinde erişilebilirdir.
- Tek instance varsayılır (Aspire dev); çoklu-instance'ta süreç-içi kilit yetmez, o gün ayrıca ele alınır.
- K8s CronJob alternatifi deploy hedefi netleşince tartılır (`todo-k8s-aspire-deploy`); bu feature onu
  öne almaz.
- Feed ucu mock'tur; yayın semantiği (idempotent yazım, önce publish sonra save) 007'deki gibidir.
- 007'nin "erişilemeyen feed hata üretmez" kuralı bilinçli evrilir: erişilemeyen feed artık exception'dır
  (US3 böyle çalışır); boş feed hatasız kapanmaya devam eder. Manuel uçta exception loglanır, kontrat aynı.