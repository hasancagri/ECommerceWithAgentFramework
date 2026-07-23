# Feature Specification: Ana Sayfa Ürün Listesinin Storefront Vitrininden Beslenmesi

**Feature Branch**: `006-home-storefront-list`

**Created**: 2026-07-23

**Status**: Draft

**Input**: User description: "Ana sayfa ürün listesi Storefront read model'inden beslensin; fat event
Price/Description/Brand ile zenginleşsin; Storefront'a liste ucu, WebApp'e StorefrontService gelsin."

**Kademe**: Tam — servisler-arası event kontratı genişliyor ve yeni bir okuma ucu kontratı ekleniyor.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Vitrin tek kaynaktan dolar (Priority: P1)

Müşteri ana sayfayı açtığında ürün kartları tek bir vitrin kaynağından gelir.
Kartta ad, açıklama, marka, fiyat ve görsel bugünkü gibi eksiksiz görünür.

**Why this priority**: Sayfanın var oluş amacı; vitrin kaynağı fiyat/açıklama/marka taşımadan geçiş yapılamaz.

**Independent Test**: Ana sayfa açılır; kartlardaki alanlar vitrin kaydındaki değerlerle birebir doğrulanır.

**Acceptance Scenarios**:

1. **Given** vitrinde dolu ürün satırları var, **When** ana sayfa açılır, **Then** kartlar ad, açıklama, marka, fiyat ve görselle listelenir.
2. **Given** vitrin boş, **When** ana sayfa açılır, **Then** "ürün bulunamadı" durumu bugünkü gibi gösterilir.

---

### User Story 2 - Ürün değişikliği vitrine kendiliğinden yansır (Priority: P2)

Ürün oluşturulduğunda, güncellendiğinde veya silindiğinde vitrin satırı elle müdahale olmadan güncellenir.
Fiyat, açıklama ve marka değişiklikleri de artık bu yansımaya dahildir.

**Why this priority**: Vitrinin güncelliği; yansıma çalışmazsa müşteri bayat fiyat görür.

**Independent Test**: Bir ürünün fiyatı değiştirilir; kısa süre içinde ana sayfa kartında yeni fiyat görülür.

**Acceptance Scenarios**:

1. **Given** vitrinde bir ürün var, **When** fiyatı güncellenir, **Then** ana sayfa kartı kısa sürede yeni fiyatı gösterir.
2. **Given** vitrinde bir ürün var, **When** ürün silinir, **Then** ürün ana sayfa listesinde artık görünmez.

---

### User Story 3 - Kartta stok ve indirim bilgisi (Priority: P3)

Müşteri ana sayfa kartında ürünün stok durumunu ve varsa indirim oranını görür.
Bu bilgi bugün ana sayfada hiç yoktu; vitrine geçişin görünür kazanımıdır.

**Why this priority**: Değer katar ama geçişin ön şartı değildir; P1/P2 tamamsa vitrin zaten çalışıyordur.

**Independent Test**: Stoklu+indirimli bir ürünün kartında stok ve indirim rozeti doğrulanır.

**Acceptance Scenarios**:

1. **Given** ürünün stok adedi ve indirimi vitrine yansımış, **When** ana sayfa açılır, **Then** kartta stok durumu ve indirim oranı görünür.
2. **Given** ürünün stok bilgisi henüz raporlanmamış (bilinmiyor), **When** kart çizilir, **Then** stok rozeti gösterilmez; kart hatasız kalır.

---

### Edge Cases

- Katalog verisi henüz yansımamış kısmi satır (ad/fiyat yok): ana sayfada gösterilmez; sayfa hatasız kalır.
- Silinmiş ürün satırı: listede yer almaz.
- Stok "bilinmiyor" (hiç raporlanmadı) ile "stok 0" ayrıdır: bilinmeyende rozet yok, sıfırda "stokta yok" gösterilir.
- Vitrin kaynağı geçici olarak erişilemezse ana sayfa mevcut hata sayfası davranışını korur.
- Eski (fiyatsız) vitrin satırları: dev ortamında veri sıfırlama + aktarım yeniden koşusuyla zenginleşir; kod tarafında geriye dönük doldurma yoktur.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Ana sayfa ürün listesi yalnızca vitrin okuma modelinden beslenir; katalog listesine ayrıca çağrı yapılmaz.
- **FR-002**: Vitrin ürün satırı ad, açıklama, marka, fiyat, görsel, stok adedi ve indirim oranını taşır.
- **FR-003**: Ürün oluşturma/güncelleme/silme bildirimleri fiyat, açıklama ve marka bilgisini de taşır; vitrin geri çağrı yapmadan beslenir.
- **FR-004**: Vitrin ürün listesi kimlik doğrulaması gerektirmeden okunabilir (mevcut anonim okuma duruşuyla uyumlu).
- **FR-005**: Katalog verisi henüz yansımamış kısmi satırlar ana sayfa listesine dahil edilmez.
- **FR-006**: Silinmiş ürünler ana sayfa listesinde yer almaz.
- **FR-007**: Satışa-açıklık (IsAvailableForSale) filtresi uygulanmaz; silinmemiş tüm dolu satırlar listelenir.
- **FR-008**: Ürün detay, sepet ve sipariş akışları mevcut kaynak ve davranışlarıyla değişmeden kalır.
- **FR-009**: Kartta stok bilinmiyorsa rozet gösterilmez; stok 0 ise "stokta yok" gösterilir; indirim yoksa indirim rozeti çizilmez.

### Key Entities

- **Vitrin ürün satırı**: Ürün başına tek birleşik kayıt; mevcut ad/görsel/stok/indirim alanlarına açıklama, marka ve fiyat eklenir.
- **Ürün değişikliği bildirimi**: Katalog kaynaklı olay sözleşmesi; fiyat, açıklama ve marka alanlarıyla genişler (bilinçli paylaşılan sözleşme).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Ana sayfa ürün kartları tek bir okuma çağrısıyla dolar; kart başına ek stok/indirim çağrısı yapılmaz.
- **SC-002**: Fiyat/açıklama/marka değişikliği, kullanıcı müdahalesi olmadan 5 saniye içinde ana sayfaya yansır.
- **SC-003**: Dolu satırlar için kartların %100'ünde ad, açıklama, marka, fiyat ve görsel eksiksiz görünür.
- **SC-004**: Mevcut alışveriş akışları (detay, sepet, sipariş) regresyonsuz çalışır; davranış değişikliği gözlenmez.

## Assumptions

- Sayfalama kapsam dışıdır; bugünkü ana sayfa gibi tüm liste tek seferde gösterilir.
- Satışa-kapalı ürünlerin gizlenmesi ayrı bir gelecek feature'dır; burada tüm ürünler listelenir.
- Silinmiş ürünlerin gizlenmesi bugünkü katalog listesi davranışıyla uyumlu kabul edilir.
- Marka değerleri mevcut sabit marka kümesiyle sınırlıdır; yeni marka tanımı bu kapsamda değildir.
- Eski satırların zenginleşmesi dev ortamında veri sıfırlama + tedarikçi aktarımının yeniden koşusuyla sağlanır; üretim tarzı backfill istenmez.
- Vitrin beslemesi yalnızca olay tabanlıdır; vitrin hiçbir kaynağa geri çağrı yapmaz (mevcut duruş korunur).