# Feature Specification: Product Sale Readiness (Completeness Gating + Agent Enrichment)

**Feature Branch**: `001-product-sale-readiness`

**Created**: 2026-07-12

**Status**: Draft

**Input**: User description: "Catalog: eksik bilgili ürünler satışta olmaz ve bir agent bunları tamamlar. SeedData 200 ürünü kasten eksik (Description = \"\", ImageUrl = null) oluşturuyor; ürün ancak bilgileri tamamsa satışa çıkabilir. Tamam = Description dolu VE ImageUrl dolu. Satışta = IsActive (admin aç/kapa) VE tamamlık; iki kavram ayrı. Tamamlık kuralı Product aggregate içinde invariant. Eksik bilgiyi ayrı bir agent tamamlar; tamamlanınca ürün satışa çıkar."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Eksik ürünler satışta görünmez (Priority: P1)

Bir müşteri (veya müşteri adına çalışan alışveriş asistanı agent) katalogda ürün ararken,
yalnızca **satışa hazır** ürünleri görür. Açıklaması veya görseli eksik olan bir ürün,
müşteri araması ve satın alma akışında hiç görünmez — çünkü bilgisi eksik bir ürün henüz
satılacak durumda değildir.

**Why this priority**: Bu, feature'ın çekirdek iş kuralıdır ve tek başına değer üretir.
Bu kural olmadan, seed edilen 200 eksik ürün müşterilere açıklamasız/görselsiz olarak
satışa çıkar — kötü bir müşteri deneyimi ve yanlış katalog. Bu tek slice bile teslim
edilse sistem tutarlı bir katalog sunar (eksikler saklı, tamlar satışta).

**Independent Test**: Katalog eksik ve tam ürünlerin karışımıyla doldurulur; müşteri
araması/listelemesi çağrılır; sonucun **yalnızca** tam (ve aktif) ürünleri içerdiği
doğrulanır. Eksik ürünler dışarıda kalır.

**Acceptance Scenarios**:

1. **Given** açıklaması boş veya görseli olmayan bir ürün, **When** müşteri o ürünü ada göre arar, **Then** ürün sonuçlarda görünmez.
2. **Given** hem açıklaması hem görseli dolu ve aktif bir ürün, **When** müşteri o ürünü ada göre arar, **Then** ürün sonuçlarda görünür.
3. **Given** açıklaması ve görseli dolu **ama** admin tarafından pasife alınmış (deaktif) bir ürün, **When** müşteri arar, **Then** ürün satışta görünmez (tamlık tek başına yetmez; aktiflik de gerekir).
4. **Given** tam ve aktif bir ürün, **When** açıklaması sonradan boşaltılır, **Then** ürün otomatik olarak satıştan düşer.

---

### User Story 2 - Agent eksik ürünleri tamamlar ve satışa çıkarır (Priority: P2)

Bir zenginleştirme süreci (enrichment agent) eksik bilgili ürünleri tespit eder,
eksik olan açıklama ve görsel bilgilerini üretip ürüne ekler. Bir ürünün açıklaması **ve**
görseli tamamlandığı anda, ürün satışa hazır hale gelir ve (aktifse) müşteri aramalarında
görünmeye başlar.

**Why this priority**: US1 katalogu tutarlı kılar ama eksik ürünler sonsuza dek satış dışı
kalır. Bu slice, eksik envanteri otomatik olarak satılabilir hale getirerek katalogu
canlandırır. US1'in üstüne inşa edilir ve bağımsız olarak gösterilebilir.

**Independent Test**: Eksik bir ürün alınır; zenginleştirme süreci çalıştırılır; ürünün
açıklama+görselinin dolduğu ve artık müşteri aramasında satışta göründüğü doğrulanır.

**Acceptance Scenarios**:

1. **Given** açıklaması ve görseli eksik bir ürün, **When** zenginleştirme süreci o ürünü işler, **Then** ürünün açıklaması ve görseli dolar.
2. **Given** zenginleştirme süreci bir ürünün açıklama+görselini tamamladı, **When** ürün aktif durumdaysa, **Then** ürün otomatik olarak müşteri aramalarında satışta görünür.
3. **Given** zenginleştirme yalnızca açıklamayı doldurup görseli dolduramadı, **When** süreç biter, **Then** ürün hâlâ eksik sayılır ve satışta görünmez.

---

### User Story 3 - Operasyon eksik/tam envanteri görebilir (Priority: P3)

Bir operatör/admin, kataloğun sağlığını görmek için hangi ürünlerin eksik (satışa hazır
değil) ve hangilerinin tam olduğunu ayırt edebilir. Bu görünürlük, zenginleştirmenin
ilerleyişini ve kaç ürünün hâlâ satış dışı olduğunu izlemeyi sağlar.

**Why this priority**: Operasyonel görünürlük değerlidir ama çekirdek müşteri değeri için
zorunlu değildir. US1/US2 olmadan anlamsızdır, bu yüzden en düşük önceliktedir.

**Independent Test**: Admin listelemesi çağrılır; her ürünün satışa-hazır (tam) olup
olmadığının sonuçta ayırt edilebildiği doğrulanır. Eksik ürünler admin görünümünde gizli
değildir; yalnızca durumları işaretlidir.

**Acceptance Scenarios**:

1. **Given** eksik ve tam ürünlerin karışımı, **When** admin tüm ürünleri listeler, **Then** liste her iki türü de içerir ve her ürünün satışa-hazır olup olmadığı ayırt edilebilir.

---

### Edge Cases

- **Sadece boşluk içeren açıklama**: Yalnızca boşluk/whitespace karakterlerinden oluşan bir açıklama "dolu" sayılmaz — eksik kabul edilir.
- **Boş string vs null görsel**: Hem `null` hem boş/whitespace görsel değeri "eksik" sayılır.
- **Kısmi tamlık**: Açıklama dolu ama görsel boş (veya tersi) → ürün eksik; satışta görünmez.
- **Sonradan bozulma**: Tam bir ürünün açıklama/görseli sonradan boşaltılırsa ürün satıştan düşmeli (kural her durum değişiminde geçerli, yalnızca ilk oluşturmada değil).
- **Zaten satılmış eksik ürün yok**: Eksik ürün hiç satışa çıkmadığından, "eksik ama sepette/siparişte" durumu bu feature'ın kapsamı dışıdır (kural satış öncesi görünürlüğü yönetir).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem bir ürünü "tam/satışa hazır bilgiye sahip" saymalı **ancak ve ancak** açıklaması boş/whitespace değilse **VE** görseli null veya boş/whitespace değilse.
- **FR-002**: Sistem bir ürünü "satışta" (müşteriye satın alınabilir) saymalı **ancak ve ancak** ürün hem tam (FR-001) hem de aktif (admin tarafından etkinleştirilmiş) ise. Tamlık ve aktiflik ayrı kavramlardır ve biri diğerinin yerine geçmez.
- **FR-003**: Müşteriye ve müşteri adına çalışan asistan aramasına dönen ürün sonuçları, satışta **olmayan** (eksik ya da pasif) ürünleri içermemelidir.
- **FR-004**: Tamlık kuralı ürün aggregate'inin **içinde** bir invariant olarak korunmalı; ürünün satışa-hazır durumu, açıklama/görselin her değişiminde (oluşturma ve güncelleme dahil) tutarlı olmalıdır — bu kural çağıran katmanda tekrar edilmemelidir.
- **FR-005**: Zenginleştirme süreci, eksik bilgili ürünleri belirleyip her biri için eksik açıklama ve/veya görseli üretip ürüne ekleyebilmelidir.
- **FR-006**: Bir ürünün açıklaması ve görseli tamamlandığında, ürün (aktifse) ek bir manuel adım gerektirmeden satışa-hazır duruma geçmeli ve müşteri aramalarında görünür olmalıdır.
- **FR-007**: Operasyon/admin görünümü, satışta olmayan ürünleri gizlememeli; bunun yerine her ürünün satışa-hazır (tam) olup olmadığını ayırt edilebilir kılmalıdır.
- **FR-008**: Zenginleştirmenin yalnızca kısmi bilgi üretmesi (ör. yalnızca açıklama) durumunda ürün eksik kalmaya devam etmeli ve satışta görünmemelidir.

### Key Entities *(include if feature involves data)*

- **Product (Ürün)**: Katalogdaki satılabilir kalem. Bu feature için ilgili nitelikleri: ad, **açıklama**, fiyat, SKU, marka, **görsel**, ve aktiflik durumu (admin aç/kapa). "Satışa hazır/tam" durumu bu niteliklerden (açıklama + görsel doluluğu) **türetilir**, ayrı bir yetki değildir. "Satışta" = tam VE aktif.
- **Enrichment Process (Zenginleştirme Süreci)**: Eksik ürünleri tespit edip eksik açıklama/görsel üreten ve ürüne yazan otomatik süreç (agent). Ürünün nasıl güncelleneceği kataloğun mevcut ürün-güncelleme davranışını kullanır; süreç yeni bir iş kuralı eklemez, yalnızca eksik alanları besler.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Müşteri araması/listelemesi hiçbir koşulda eksik (açıklama veya görsel içermeyen) ürün döndürmez — eksik ürünlerin müşteriye görünürlüğü %0.
- **SC-002**: Bir ürünün açıklaması **ve** görseli tamamlandıktan sonra, ürün (aktifse) ek bir manuel yayına-alma adımı olmadan müşteri aramalarında satışta görünür.
- **SC-003**: Seed edilen 200 ürünün başlangıçta hiçbiri satışta görünmez (hepsi eksik); zenginleştirme çalıştıkça satışa çıkan ürün sayısı, tamamlanan ürün sayısıyla birebir artar.
- **SC-004**: Tam ama pasif (deaktif) bir ürün müşteri aramasında görünmez — yani tamlık, aktifliğin yerine geçmez (iki kavramın ayrılığı gözlemlenebilir).
- **SC-005**: Admin listelemesi hem eksik hem tam ürünleri içerir ve her ürünün satışa-hazır olup olmadığı ayırt edilebilir.

## Assumptions

- **Tamlık tanımı iki alanla sınırlı**: Yalnızca açıklama ve görsel doluluğu satışa-hazırlığı belirler; ad/fiyat/SKU zaten oluşturmada zorunlu kabul edilir ve bu feature'ın kapsamı dışındadır.
- **"Satışta" mevcut aktiflik kavramını korur**: Bugün ürünlerin bir aktiflik durumu (admin aç/kapa) vardır; bu feature onu değiştirmez, üzerine tamlık şartını ekler.
- **Müşteri arama akışı mevcut**: Müşteri/asistan araması bugün de aktiflik filtreliyor; bu feature filtreye tamlık şartını da ekler.
- **Zenginleştirme yeni iş kuralı eklemez**: Agent, kataloğun mevcut ürün-güncelleme yolunu kullanarak eksik alanları besler; satışa-hazırlık kararı yine aggregate'in invariant'ıdır.
- **Otomatik yayın**: Zenginleştirme tamamlandığında ürün, insan onayı beklemeden (aktifse) satışa çıkar — kullanıcı akışı "tamamlanınca satışa çıkar" olarak tanımladı.

## Deferred to Planning (HOW — bu spec'in kapsamı dışı)

Aşağıdakiler NE/NEDEN değil NASIL sorularıdır; `/speckit-plan` aşamasına bırakılmıştır:

- Zenginleştirme agent'ının yapısı: yeni ayrı bir agent projesi mi (ör. `src/agents` altında ikinci proje) yoksa mevcut ChatAgent içinde bir akış mı.
- Agent'ın açıklama/görsel içeriğini nereden/nasıl ürettiği (LLM üretimi, görsel kaynağı, kalite).
- Tetikleme şekli: manuel, toplu (batch), veya zamanlanmış (scheduled).
- Tamlık durumunun sorgulanabilirlik için kalıcı bir alan olarak mı tutulacağı yoksa açıklama/görsel alanlarından mı filtreleneceği.