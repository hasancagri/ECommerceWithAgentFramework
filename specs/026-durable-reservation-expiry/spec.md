# Feature Specification: Durable Rezervasyon Süre-Sonu

**Feature Branch**: `026-durable-reservation-expiry`

**Created**: 2026-08-05

**Status**: Draft

**Input**: User description: "Stock rezervasyon TTL temizliğini poll-eden Hangfire cron
yerine durable scheduled message'a çevir — tam TTL anında serbest bırak, restart'a dayanıklı."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Rezervasyon tam süresinde serbest kalır (Priority: P1)

Bir alışverişçi ürünü sepete alınca stoktan rezervasyon tutulur. Süresi (TTL) dolduğu anda
rezervasyon serbest bırakılır: sepet satırı temizlenir ve stok yeniden uygun hale gelir —
bir sonraki periyodik taramayı beklemeden, gecikmesiz.

**Why this priority**: Çekirdek amaç. Poll penceresi bayat rezervasyon/sepet ve yanlış
"stok yok" görünümüne yol açar; tam-anında serbest bırakma bunu kapatır.

**Independent Test**: Kısa TTL ile rezervasyon oluştur; TTL anında sepet satırının silindiği
ve stoğun tekrar uygun olduğu, periyodik tarama olmadan doğrulanır.

**Acceptance Scenarios**:

1. **Given** aktif bir rezervasyon, **When** TTL anı gelir, **Then** rezervasyon birkaç
   saniye içinde serbest bırakılır (sepet satırı silinir, stok uygun olur).
2. **Given** bir rezervasyon süresi dolar, **When** serbest bırakılır, **Then** sepet
   satırının kaldırılması için bir "rezervasyon süresi doldu" bildirimi üretilir (mevcut
   sözleşme yeniden kullanılır).

---

### User Story 2 - Yenilenen rezervasyon erken serbest kalmaz (Priority: P1)

Alışverişçi sepette adet değiştirir ya da süre yenilenirse, rezervasyon yeni bitiş anına
taşınır; daha önce planlanmış (bayat) süre-sonu tetiği rezervasyonu erkenden boşaltmaz.

**Why this priority**: Yanlış erken serbest bırakma sepetten ürün kaybı/oversell riski
doğurur; yenileme güvenli olmalı.

**Independent Test**: Rezervasyonu oluştur, TTL'den önce yenile; eski bitiş anı geldiğinde
rezervasyonun HÂLÂ aktif olduğu ve boşaltılmadığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** yenilenmiş bir rezervasyon, **When** önceki (bayat) bitiş anı gelir, **Then**
   rezervasyon aktif kalır, hiçbir şey serbest bırakılmaz (no-op).
2. **Given** yenilenmiş rezervasyon, **When** yeni bitiş anı gelir, **Then** rezervasyon
   normal şekilde serbest bırakılır.

---

### User Story 3 - Yeniden başlatmaya dayanıklılık (Priority: P1)

Sistem, rezervasyon oluşturulduktan sonra ama TTL dolmadan önce yeniden başlatılsa bile,
süre-sonu tetiği kaybolmaz; rezervasyon yine (en geç TTL anında ya da açılışta) serbest
bırakılır.

**Why this priority**: Bellek-içi zamanlayıcı restart'ta kaybolur → kalıcı bayat rezervasyon.
Dayanıklılık şart.

**Independent Test**: Rezervasyon oluştur, TTL'den önce servisi yeniden başlat, TTL anını
bekle; rezervasyonun yine serbest bırakıldığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** planlanmış bir süre-sonu tetiği, **When** servis yeniden başlar, **Then** tetik
   korunur ve rezervasyon yine serbest bırakılır.

---

### Edge Cases

- Tetik ateşlendiğinde rezervasyon zaten yok/temizlenmişse: hiçbir şey yapılmaz (idempotent,
  no-op).
- Aynı stok için birden çok rezervasyon: yalnız süresi geçmiş olanlar serbest bırakılır,
  aktifler korunur.
- Serbest bırakma kalıcı olarak başarısız olursa (tetik tekrarları tükenir, DLQ): seyrek
  periyodik güvenlik-ağı taraması (~10 dk) bu bayat rezervasyonu yine de yakalar. Durable
  tetik birincil, tarama yalnız kaçanları toplar; ikisi de aynı idempotent serbest bırakmayı
  kullanır, çakışmaz.
- Eşzamanlı değişim (rezervasyon serbest bırakılırken güncelleme): tutarlılık korunur, çakışan
  serbest bırakma bir sonraki fırsatta çözülür.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, her rezervasyon oluşturulduğunda/yenilendiğinde onun bitiş anına (TTL)
  bağlı bir süre-sonu tetiği planlamalıdır.
- **FR-002**: Süre-sonu tetiği, TTL anında (birkaç saniye tolerans) ateşlenmeli; periyodik
  tarama beklenmeden ilgili stoğun süresi geçmiş rezervasyonlarını serbest bırakmalıdır.
- **FR-003**: Serbest bırakma **idempotent** olmalı: tetik ateşlendiğinde rezervasyon aktif
  değilse/yoksa hiçbir yan etki olmadan no-op dönmelidir.
- **FR-004**: Süresi dolmuş rezervasyon serbest bırakıldığında, sepet satırının silinmesi için
  mevcut "rezervasyon süresi doldu" bildirimi yayınlanmalıdır (yeni sözleşme eklenmez).
- **FR-005**: Yenileme, aynı stok için yeni bir tetik planlamalı; önceden planlanmış bayat
  tetik ateşlense bile rezervasyon hâlâ aktifse onu serbest bırakmamalıdır (ayrı bir
  nesil-belirteci gerekmeden, aktiflik kontrolüyle).
- **FR-006**: Planlanmış tetikler **kalıcı** olmalı: servis yeniden başlatıldığında
  kaybolmamalı; rezervasyon yine en geç TTL anında serbest bırakılmalıdır.
- **FR-007**: Çözüm **yalnız Stock context'i** içinde yer almalı; yeni tablo, yeni servisler-
  arası sözleşme ya da yeni bildirim tipi eklenmemelidir (mevcut olanlar yeniden kullanılır).
- **FR-008**: Birincil mekanizma durable tetik olsa da, kalıcı-başarısız (DLQ) tetiklerin
  bıraktığı bayat rezervasyonları toplamak için seyrek periyodik güvenlik-ağı taraması
  (~10 dk) korunmalıdır; tarama da aynı idempotent serbest bırakmayı kullanır.

### Key Entities

- **Rezervasyon**: Bir stok için tutulan, bir kullanıcıya ait, tek bir bitiş anı (TTL) olan
  geçici tutma. Aktif ya da süresi geçmiş olabilir.
- **Süre-sonu tetiği**: Bir rezervasyonun bitiş anına planlanmış, kalıcı, tek-atımlık işaret;
  ateşlendiğinde ilgili stoğun süresi geçmiş rezervasyonlarını serbest bırakır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Süresi dolan bir rezervasyon, TTL anından itibaren ≤ 5 saniye içinde serbest
  bırakılır (periyodik tarama aralığına bağlı gecikme yok).
- **SC-002**: Serbest kalan stok, TTL anından itibaren ≤ 5 saniye içinde diğer alışverişçiler
  için yeniden uygun görünür.
- **SC-003**: TTL öncesi yeniden başlatmada, rezervasyonların %100'ü yine serbest bırakılır
  (kalıcı tetik kaybı yok).
- **SC-004**: Yenilenen rezervasyonların hiçbiri bayat tetik nedeniyle erken serbest kalmaz
  (%0 yanlış-pozitif).

## Assumptions

- Rezervasyon serbest bırakma mantığı (süresi geçmişleri temizleme, idempotent) ve "rezervasyon
  süresi doldu" bildirimi hâlihazırda mevcuttur; bu feature yalnız TETİKLEME mekanizmasını
  poll'dan tam-anlı dayanıklı tetiğe çevirir.
- Rezervasyon TTL değeri ve bitiş anı zaten biliniyor (rezervasyon kurulurken belirleniyor).
- Dayanıklı zamanlanmış tetik altyapısı mevcuttur (kalıcı yerel kuyruk).
- "Birkaç saniye tolerans" (≤ 5 sn) kabul edilebilir; milisaniye kesinliği gerekmez.
- Stok BC dışına (Basket vb.) yalnız mevcut bildirim üzerinden etki edilir; doğrudan erişim yok.