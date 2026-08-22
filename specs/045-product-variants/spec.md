# Feature Specification: Ürün Varyantları (Barkod Ailesi)

**Feature Branch**: `045-product-variants`

**Created**: 2026-08-22

**Status**: Draft

**Input**: User description: "Ürün varyantları (Variants) — aynı modelin renk/beden gibi varyantları
tedarikçi feed'inden AYRI barkodlarla gelir; kombinasyon üretme YOK, mevcut ürünler barkod-ailesi
olarak gruplanır. Aile anahtarı: feed satırına opsiyonel aile kodu (familyCode) alanı. Varyant
ekseni 043 spec registry'sindeki mevcut özellikler. Vitrin: listede aile TEK kart, detayda varyant
seçici; ailesiz ürünler bugünkü gibi. nopCommerce grouped-product yalnız akıl referansı."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Feed'den aile kurulumu (Priority: P1)

Tedarikçi feed satırları opsiyonel aile kodu taşır; aynı kodu taşıyan ürünler (ayrı barkodlar)
sistemde otomatik olarak tek bir varyant ailesi olur. Kod taşımayan ürün ailesiz kalır.

**Why this priority**: Aile bilgisi doğmadan hiçbir vitrin davranışı kurulamaz; veri temeli budur.
Kombinasyon üretimi yoktur — yalnız mevcut ürünler gruplanır.

**Independent Test**: Mock feed'e iki ürüne aynı aile kodu ekle → çekim sonrası iki ürün aynı
ailede; kodsuz ürün ailesiz. Vitrin dokunuşu olmadan veri üzerinden doğrulanır.

**Acceptance Scenarios**:

1. **Given** feed'de üç satır aynı aile kodunu taşıyor, **When** feed çekilir ve yayın olur,
   **Then** üç ürün aynı ailededir ve her biri kendi barkod/fiyat/stok/özelliklerini korur.
2. **Given** feed satırında aile kodu yok, **When** yayın olur, **Then** ürün ailesiz kalır ve
   tüm mevcut davranışlar değişmez.
3. **Given** iki tedarikçi aynı barkoda farklı aile kodu veriyor, **When** birleşme çalışır,
   **Then** öncelikli tedarikçinin dolu değeri kazanır (alan bazında, sıra-bağımsız).
4. **Given** üründe aile kodu vardı, feed'in yeni revizyonunda kaldırıldı, **When** çekim olur,
   **Then** ürün aileden çıkar ve ailesiz davranışa döner.

---

### User Story 2 - Detayda varyant seçici (Priority: P2)

Müşteri, aileli bir ürünün detay sayfasında ailenin diğer üyelerini varyant seçiciyle görür
(ör. Renk: Siyah/Beyaz, Beden: S/M/L) ve seçim yapınca o üyenin detayına geçer. Fiyat, stok,
yorum ve özellikler her üyenin KENDİSİNE aittir.

**Why this priority**: Varyantın müşteri değeri seçim deneyimidir; aile verisi (US1) olmadan
çalışamaz ama liste gruplaması (US3) olmadan da tek başına değer taşır.

**Independent Test**: Elle aile kurulmuş ürünlerden birinin detayı açılır → seçici, ailedeki
üyeleri ayırt eden özellik değerleriyle listelenir; seçim diğer üyenin sayfasına götürür.

**Acceptance Scenarios**:

1. **Given** üç üyeli aile yalnız Renk özelliğiyle ayrışıyor, **When** bir üyenin detayı açılır,
   **Then** seçicide üç renk görünür, mevcut üyeninki seçili işaretlidir.
2. **Given** seçicide başka bir renk seçildi, **When** geçiş olur, **Then** o üyenin detayı
   (kendi fiyat/stok/özellik/yorumları) görüntülenir.
3. **Given** aile üyeleri iki eksende ayrışıyor (Renk + Beden), **When** detay açılır,
   **Then** her eksen ayrı seçici grubu olarak görünür.
4. **Given** ürün ailesiz, **When** detay açılır, **Then** varyant seçici hiç görünmez.
5. **Given** bir üyenin stok durumu "stokta yok", **When** seçici çizilir, **Then** o üye
   seçilebilir kalır ama stokta olmadığı görsel olarak belli olur.

---

### User Story 3 - Listede aile tek kart (Priority: P3)

Ürün liste/vitrin yüzeylerinde bir aile TEK kartla (temsilci üye) görünür; kartta varyant
çeşitliliği hissettirilir (ör. "3 renk"). Ailesiz ürünler bugünkü gibi ayrı kart kalır.

**Why this priority**: Liste kalabalığını çözer ve keşfi iyileştirir; ancak US1+US2 olmadan
temsilcinin götüreceği detay deneyimi eksik kalır.

**Independent Test**: Aynı aileden üç ürün varken liste açılır → tek kart; karta tıklayınca
temsilci üyenin detayı (seçicili) açılır; ailesiz ürün sayısı değişmez.

**Acceptance Scenarios**:

1. **Given** üç üyeli aile ve beş ailesiz ürün, **When** liste açılır, **Then** altı kart görünür
   (1 aile + 5 ailesiz).
2. **Given** aile üyelerinden yalnız biri stokta, **When** liste açılır, **Then** kartın temsilcisi
   stokta olan üyedir.
3. **Given** özellik filtresi "Renk: Siyah" seçili ve ailenin yalnız bir üyesi siyah,
   **When** liste çizilir, **Then** aile o üyeyle temsil edilir; filtre sayıları birebir kalır.
4. **Given** sayfalı liste, **When** gruplama uygulanır, **Then** sayfa boyutu kart adedine göre
   tutarlıdır (bir aile bir kart sayılır).

---

### Edge Cases

- Tek üyeli aile (kod var ama tek ürün): seçici gösterilmez, listede tek kart — ailesiz gibi görünür.
- Aile üyesi vitrinden kalkarsa (delist/yayın dışı): seçiciden ve temsilci adaylığından düşer;
  kalan tek üye kalırsa tek-üye davranışına döner.
- Üyeler hiçbir kayıtlı özellikle ayrışmıyorsa (özellik eksik/aynı): seçici üyeleri ürün adıyla listeler.
- Farklı tedarikçilerin aile kodu çakışması: alan-bazlı öncelik birleşmesi karar verir (US1-3).
- Aile kodu değişen ürün: eski aileden çıkar, yeni aileye girer (takip eden yayınla).
- Puan/yorum (044) üye-bazlıdır: kartta temsilci üyenin puanı görünür; birleşik aile puanı yoktur.
- Sepet/stok/sipariş üye-bazlı çalışmaya devam eder; aile yalnız görüntüleme gruplamasıdır.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Tedarikçi feed satırı OPSİYONEL bir aile kodu taşıyabilmeli; kod taşıyan ürünler
  aynı ailede gruplanmalı, taşımayanlar ailesiz kalmalı.
- **FR-002**: Aile üyeliği yalnız MEVCUT ürünleri gruplar; sistem hiçbir kombinasyon/yeni ürün üretmemeli.
- **FR-003**: Aile kodu, çok-tedarikçili birleşmede diğer içerik alanlarıyla aynı kurala uymalı
  (alan bazında öncelik kazanır, sıra-bağımsız); kod değişimi/kaldırılması sonraki yayınla yansımalı.
- **FR-004**: Varyant ekseni ayrı bir model olmamalı; üyeleri ayırt eden boyutlar mevcut kayıtlı
  özelliklerden (Renk, Beden vb.) türetilmeli.
- **FR-005**: Ürün detayında aileli üründe varyant seçici görünmeli: ayrışan her özellik bir grup,
  her üyenin değeri seçilebilir; seçim ilgili üyenin detayına götürmeli. Ailesizde seçici olmamalı.
- **FR-006**: Seçicide mevcut üye işaretli olmalı; stokta olmayan üye seçilebilir ama ayırt edilir olmalı.
- **FR-007**: Liste/vitrin yüzeylerinde aile TEK kartla temsil edilmeli; temsilci stokta olan
  üyelerden seçilmeli (varsayılan kural: stokta en düşük fiyatlı; hiçbiri stokta değilse deterministik bir üye).
- **FR-008**: Kartta ailenin varyant çeşitliliği hissettirilmeli (ör. üye/renk adedi rozeti).
- **FR-009**: Filtre/arama bir aile üyesiyle eşleşiyorsa aile o üyeyle temsil edilmeli; filtre
  sayıları görünen kart davranışıyla tutarlı kalmalı.
- **FR-010**: Sayfalama kart (aile=1) bazında tutarlı olmalı; toplam sayılar gruplamayı yansıtmalı.
- **FR-011**: Ailesiz ürünlerin tüm mevcut davranışları (liste, detay, sepet, yorum, öneri)
  DEĞİŞMEMELİ; aile yalnız görüntüleme gruplamasıdır, sepet/stok/sipariş üye-bazlı kalmalı.
- **FR-012**: Mock tedarikçi verisi elle düzenlenebilir dosyalarda kalmalı; aile kodu örnekleri
  (çok üyeli, tek üyeli, kodsuz, tedarikçi-çakışmalı) örnek veride bulunmalı.

### Key Entities

- **Varyant Ailesi (Family)**: Aile koduyla tanımlanan ürün grubu; kendisi bir ürün değildir,
  üyelerin üstünde yaşayan bir gruplama kimliğidir.
- **Aile Üyesi**: Mevcut ürün (kendi barkod/fiyat/stok/özellik/yorumuyla); aynı anda en fazla
  BİR aileye üye olabilir.
- **Varyant Ekseni**: Üyeleri birbirinden ayıran kayıtlı özellik boyutu (Renk, Beden...);
  aileden türetilir, ayrıca saklanan bir tanım değildir.
- **Temsilci Üye**: Liste kartında aileyi temsil eden üye; stok/fiyat kuralıyla seçilir,
  filtre bağlamında eşleşen üyeye kayar.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Feed'e aile kodu eklenen ürün grupları, elle hiçbir müdahale olmadan (%100 otomatik)
  bir sonraki çekim+yayın döngüsünde aile olarak görünür.
- **SC-002**: Aileli ürün detayından başka üyeye geçiş tek etkileşimle tamamlanır; seçici her
  zaman ailenin görünür tüm üyelerini kapsar (eksik üye %0).
- **SC-003**: Üç üyeli aile listede tek kart olarak görünür; liste toplamları ve filtre sayıları
  görünen kartlarla birebir tutarlıdır (fark 0).
- **SC-004**: Ailesiz ürünlerde ve mevcut akışlarda (sepet, sipariş, yorum, öneri, arama)
  regresyon 0; aile kodu olmayan feed revizyonlarıyla sistem bugünkü davranışını korur.
- **SC-005**: Aile kodunun kaldırılması/değişmesi bir sonraki yayın döngüsünde yüzeylere yansır
  (bayat aile üyeliği kalmaz).

## Assumptions

- Temsilci kuralı: stokta olan üyeler arasından en düşük fiyatlı; hiçbiri stokta değilse
  deterministik bir üye (ör. ilk yayınlanan). Ayar/elle seçim v1 dışı.
- Filtre eşleşmesinde temsilci, eşleşen üyeye kayar; birden çok üye eşleşirse temsilci kuralı
  eşleşenler arasında uygulanır.
- Arama dahil tüm liste yüzeyleri aynı gruplama davranışını kullanır (yüzeyler arası tutarlılık).
- Kart puan rozeti (044) temsilci üyenin puanıdır; birleşik aile puanı v1 kapsam dışı.
- Aile kodu tedarikçiler-arası ortak bir sözleşmedir (aynı model = aynı kod varsayımı);
  çakışmada öncelik birleşmesi son sözü söyler.
- Chat/agent yüzeyinde varyant seçimi v1 kapsam dışı; agent okumaları üye-bazlı sürer.
- Tek üyeli aile meşrudur (ilerde üye gelebilir); yüzeyde ailesiz gibi davranır.
- nopCommerce grouped-product yalnız akıl referansı; model sıfırdan tasarlanır.
