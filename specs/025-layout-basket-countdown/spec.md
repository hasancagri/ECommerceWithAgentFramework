# Feature Specification: Layout Seviyesinde Sepet Geri Sayımı

**Feature Branch**: `025-layout-basket-countdown`

**Created**: 2026-08-04

**Status**: Draft

**Input**: User description: "Layout seviyesinde sepet rezervasyon geri sayımı — ortak
header'da her sayfada tek basket-düzeyi geri sayım; sıfırda purge + tazele; Basket/Index
çakışması giderilir; sadece WebApp/frontend."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Her sayfada rezervasyon süresini görmek (Priority: P1)

Giriş yapmış ve sepetinde aktif rezervasyonu olan kullanıcı, hangi sayfada gezinirse
gezinsin (ana sayfa, ürün, arama...) ortak header'da sepetinin ne kadar süre sonra
boşalacağını MM:SS geri sayımıyla görür. Geri sayım saniyede bir azalır.

**Why this priority**: Kullanıcının asıl talebi bu; rezervasyon baskısını sepet sayfasına
girmeden her yerde görünür kılar, terk-riskini azaltır. Tek başına değer üretir.

**Independent Test**: Sepete ürün ekle, başka sayfaya git — header'da geri sayımın
göründüğü ve azaldığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** aktif rezervasyonu olan giriş yapmış kullanıcı, **When** herhangi bir sayfa
   açılır, **Then** header'da kalan süre MM:SS olarak görünür ve saniyede bir azalır.
2. **Given** header'da görünen geri sayım, **When** kullanıcı başka sayfaya geçer,
   **Then** geri sayım kaldığı süreden devam eder (sıfırlanmaz).

---

### User Story 2 - Süre bitince sepetin temizlenmesi (Priority: P1)

Geri sayım sıfıra indiğinde sepet sunucu tarafında boşaltılır ve arayüz bunu yansıtır;
kullanıcı hâlâ süresi dolmuş bir sepet görmez.

**Why this priority**: Derdin özü — "süre dolsa bile sepette gözükme riski". Bayat
gösterimi kapatır.

**Independent Test**: Rezervasyon süresini beklet (ya da kısa süre ile) — sıfırda sepetin
boşaldığı ve header sayacının kaybolduğu doğrulanır.

**Acceptance Scenarios**:

1. **Given** geri sayım sıfıra iner, **When** bu an gelir, **Then** sepet boşaltma işlemi
   tetiklenir ve header sayacı gizlenir.
2. **Given** kullanıcı sepet sayfasındayken süre biter, **When** sıfıra inilir, **Then**
   sepet sayfası boş sepeti gösterecek şekilde tazelenir.

---

### User Story 3 - Tek kanonik geri sayım (Priority: P2)

Sepet sayfasındaki mevcut ayrı geri sayım ile header sayacı çakışmaz; kullanıcı aynı anda
iki farklı/yarışan sayaç görmez.

**Why this priority**: Tutarlılık; iki sayaç kafa karıştırır ve senkron sorunları doğurur.

**Independent Test**: Sepet sayfasını aç — yalnız tek (header) geri sayımın göründüğü
doğrulanır.

**Acceptance Scenarios**:

1. **Given** kullanıcı sepet sayfasında, **When** sayfa açılır, **Then** yalnızca tek
   geri sayım (kanonik) görünür.

---

### Edge Cases

- Sepet boş ya da aktif rezervasyon yoksa: header'da geri sayım **gösterilmez**.
- Giriş yapmamış kullanıcı: geri sayım gösterilmez (sepet kullanıcıya bağlı).
- Sayfa, kalan süre çok azken açılırsa: kalan gerçek süre gösterilir; zaten geçmişse
  sepet boşaltma tetiklenir ve sayaç gösterilmez.
- İstemci saati sunucudan sapmışsa: geri sayım sunucunun mutlak bitiş anına göre hesaplanır
  (mevcut davranışla aynı); küçük sapma kabul edilir.
- Süre biterken sepet boşaltma isteği başarısız olursa: sonraki sayfa yüklemesinde/tazelemede
  yeniden denenir (boşaltma idempotenttir).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, giriş yapmış ve aktif rezervasyonu olan kullanıcı için ortak header'da
  kalan rezervasyon süresini MM:SS biçiminde göstermelidir.
- **FR-002**: Geri sayım tüm sayfalarda (ortak layout) görünmeli ve sayfa geçişlerinde
  kaldığı süreden devam etmelidir (sıfırlanmaz).
- **FR-003**: Geri sayım, sepetin mevcut basket-düzeyi mutlak bitiş anına dayanmalıdır;
  yeni backend kontratı, alan veya event eklenmez.
- **FR-004**: Geri sayım sıfıra indiğinde sistem sepeti boşaltma işlemini (mevcut purge-expired
  yeteneği) tetiklemeli ve header sayacını gizlemelidir.
- **FR-005**: Süre bitişi kullanıcıyı bulunduğu sayfadan zorla başka yere yönlendirmemeli;
  sepet sayfasındaysa boş sepeti yansıtacak şekilde tazelenmelidir.
- **FR-006**: Sepet boşsa, aktif rezervasyon yoksa veya kullanıcı giriş yapmamışsa geri sayım
  gösterilmemelidir.
- **FR-007**: Header geri sayımı tek kanonik sayaç olmalı; sepet sayfasındaki ayrı geri sayım
  ile aynı anda iki sayaç oluşmamalıdır.

### Key Entities

- **Sepet rezervasyonu**: Kullanıcının sepetine bağlı, tek bir mutlak bitiş anına
  (basket-düzeyi) sahip zaman penceresi; item-başına ayrı süre yoktur.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Aktif rezervasyonu olan kullanıcı, sepet sayfasına girmeden herhangi bir sayfada
  kalan süreyi görebilir (header'da görünür ve azalır).
- **SC-002**: Geri sayım sıfıra indikten sonra kullanıcı 1 sayfa yükleme içinde boş sepet görür;
  süresi dolmuş sepet satırı gösterilmez.
- **SC-003**: Herhangi bir anda kullanıcı en fazla tek geri sayım görür (çakışma yok).
- **SC-004**: Sayfa geçişlerinde geri sayım tutarlıdır; sapma ≤ 1 saniye.

## Assumptions

- Sepet ve rezervasyon giriş yapmış kullanıcıya bağlıdır; anonim kullanıcı kapsam dışıdır.
- Rezervasyon süresi tek basket-düzeyi mutlak bitiş anıdır (item-başına TTL yok); mevcut model
  değişmez.
- Sepet boşaltma yeteneği ve kalan süre bilgisi hâlihazırda mevcuttur; yeni backend işi yoktur.
- Süre bitince stok serbest bırakımı bu feature'ın kapsamı değildir (rezervasyonlar aynı anda
  dolar; sunucu-tarafı temizliği ayrı bir feature — durable süre-sonu — ile ele alınır).
- Yalnızca WebApp (frontend) değişir; hiçbir mikroservis kontratı/tablosu/eventi değişmez.