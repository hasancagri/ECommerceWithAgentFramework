# Feature Specification: Tüm Ürünler Ekranı ve Sayfalama

**Feature Branch**: `011-all-products-pagination`

**Created**: 2026-07-24

**Status**: Draft

**Input**: User description: "Dashboard ve ürünleri listelemede sayfalama kısmını da aradan çıkartalım.
Sayfalama ana sayfada değil, yeni Tüm Ürünler ekranında olacak; ana sayfa kısaltılıp link alacak."

**Kademe**: Küçük — tek okuma modeli; yeni tablo/aggregate/event yok. Mevcut vitrin liste ucu
sayfalama parametreleriyle genişler; belirsizlik brainstorm'da giderildi.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tüm ürünleri sayfa sayfa gezme (Priority: P1)

Müşteri yeni "Tüm Ürünler" ekranını açar; ürünler sayfa başına 12 kart olarak listelenir.
Sayfa numaraları ve Önceki/Sonraki ile sayfalar arasında gezinir.

**Why this priority**: Feature'ın var oluş amacı; sayfalı listeleme olmadan ekranın değeri yok.

**Independent Test**: 12'den fazla ürünle ekran açılır; 2. sayfaya geçilir, farklı 12 ürün doğrulanır.

**Acceptance Scenarios**:

1. **Given** vitrinde 30 ürün var, **When** Tüm Ürünler açılır, **Then** ilk 12 ürün ada göre sıralı listelenir.
2. **Given** 1. sayfadayım, **When** "2" veya "Sonraki" tıklanır, **Then** 13-24. ürünler gösterilir ve 2 vurgulanır.
3. **Given** vitrinde 30 ürün var, **When** herhangi bir sayfa açılır, **Then** pager toplam 3 sayfa gösterir.
4. **Given** vitrin boş, **When** Tüm Ürünler açılır, **Then** "ürün bulunamadı" boş durumu gösterilir.

---

### User Story 2 - Kısaltılmış dashboard ve geçiş linki (Priority: P2)

Müşteri login sonrası dashboard'da (ana sayfa) artık en fazla 8 ürün görür.
"Tüm ürünleri gör" bağlantısıyla Tüm Ürünler ekranına geçer.

**Why this priority**: Değer katar ama P1'siz anlamsız; link hedefi olan ekran önce var olmalı.

**Independent Test**: 8'den fazla ürün varken ana sayfada 8 kart ve link doğrulanır; link yeni ekrana götürür.

**Acceptance Scenarios**:

1. **Given** vitrinde 20 ürün var, **When** ana sayfa açılır, **Then** yalnız ilk 8 ürün ve tüm ürünler linki görünür.
2. **Given** ana sayfadayım, **When** linke tıklanır, **Then** Tüm Ürünler ekranının 1. sayfası açılır.
3. **Given** vitrinde 5 ürün var, **When** ana sayfa açılır, **Then** 5 ürün ve link yine görünür.

---

### Edge Cases

- Sayfa numarası 0, negatif veya sayı değilse 1. sayfa gösterilir; hata sayfasına düşülmez.
- Aralık dışı sayfa (ör. 3 sayfa varken 99): boş durum mesajı gösterilir, sayfa hatasız kalır.
- Toplam ürün 12 veya daha azsa pager gösterilmez; tek sayfa listelenir.
- Kısmi (ad/fiyat yansımamış) ve silinmiş satırlar listelenmez; sayfa sayısı buna göre hesaplanır (006 davranışı).
- Vitrin kaynağı erişilemezse mevcut hata sayfası davranışı korunur.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Yeni bir Tüm Ürünler ekranı vardır; vitrin okuma modelinden beslenir ve anonim erişilir.
- **FR-002**: Liste sayfa başına 12 ürün gösterir; sıralama ürün adına göre alfabetiktir.
- **FR-003**: Pager sayfa numaraları ve Önceki/Sonraki sunar; geçerli sayfa görsel olarak vurgulanır.
- **FR-004**: Pager toplam kayıt sayısından hesaplanır; tek sayfa varsa pager çizilmez.
- **FR-005**: Geçersiz sayfa değeri (0, negatif, sayısal olmayan) 1. sayfaya normalize edilir.
- **FR-006**: Aralık dışı sayfa isteği boş durum mesajı gösterir; teknik hata üretmez.
- **FR-007**: Ana sayfa en fazla ilk 8 ürünü gösterir ve Tüm Ürünler ekranına bağlantı sunar.
- **FR-008**: Tüm Ürünler kartları ana sayfa kartlarıyla aynı bilgiyi taşır (ad, açıklama, marka, fiyat, görsel, stok, indirim).
- **FR-009**: Kısmi ve silinmiş vitrin satırları listelenmez; mevcut filtre davranışı korunur.
- **FR-010**: Ürün detay, sepet ve sipariş akışları davranış değiştirmeden kalır.

### Key Entities

- **Vitrin ürün satırı**: Mevcut birleşik vitrin kaydı; bu feature'da alan değişikliği yoktur.
- **Sayfalı liste sonucu**: Bir sayfalık ürün kümesi + sayfa numarası, sayfa boyutu ve toplam kayıt bilgisi.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Tüm Ürünler ekranı her sayfada en fazla 12 kartla tek okuma çağrısında dolar.
- **SC-002**: Kullanıcı pager ile herhangi bir sayfaya tek tıkla ulaşır; geçiş sonrası doğru küme görünür.
- **SC-003**: Ana sayfa hiçbir durumda 8'den fazla ürün göstermez; linkten yeni ekrana geçiş çalışır.
- **SC-004**: Geçersiz/aralık dışı sayfa isteklerinin %100'ü hatasız 1. sayfa veya boş durumla sonuçlanır.
- **SC-005**: Mevcut alışveriş akışları (detay, sepet, sipariş) regresyonsuz çalışır.

## Assumptions

- Sayfa boyutu sabit 12'dir; kullanıcıya boyut seçtirme kapsam dışıdır.
- Sıralama yalnız ada göredir; ek sıralama, arama ve filtreleme kapsam dışıdır (ayrı feature).
- Ana sayfanın 8 ürünü mevcut sıralamanın ilk 8'idir; öne çıkarma/kürasyon mantığı yoktur.
- Vitrin okuması mevcut anonim okuma duruşunu korur; yeni yetki gerekmez.
- Sayfalama klasik sayfa numarasıdır; sonsuz kaydırma / "daha fazla yükle" bilinçli reddedildi.