# Feature Specification: Product Sale Readiness (Completeness Gating)

**Feature Branch**: `001-product-sale-readiness`

**Created**: 2026-07-12

**Status**: Draft

**Input**: User description: "Catalog: eksik bilgili ürünler satışta olmaz. SeedData 200 ürünü kasten eksik (Description = \"\", ImageUrl = null) oluşturuyor; ürün ancak bilgileri tamamsa satışa çıkabilir. Tamam = Description dolu VE ImageUrl dolu. Satışta = IsActive (admin aç/kapa) VE tamamlık; iki kavram ayrı. Tamamlık kuralı Product aggregate içinde invariant. Eksik bilgiyi dolduran agent AYRI bir plan/feature'da ele alınacak."

## Scope Note

Bu feature **yalnızca domain kuralını** kapsar: bilgisi eksik ürün satışa çıkamaz. Eksik
bilgiyi (açıklama/görsel) otomatik dolduran **zenginleştirme agent'ı bu spec'in kapsamı
dışındadır** ve ayrı bir feature olarak ele alınacaktır (bkz. "Out of Scope"). Bu feature,
eksikleri hangi yolla doldurulursa doldurulsun (agent, admin elle, içe aktarma) geçerli olan
satılabilirlik kuralını tanımlar.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Eksik ürünler satışta görünmez (Priority: P1)

Bir müşteri (veya müşteri adına çalışan alışveriş asistanı agent) katalogda ürün ararken,
yalnızca **satışa hazır** ürünleri görür. Açıklaması veya görseli eksik olan bir ürün,
müşteri araması ve satın alma akışında hiç görünmez — çünkü bilgisi eksik bir ürün henüz
satılacak durumda değildir.

**Why this priority**: Bu, feature'ın çekirdek iş kuralıdır ve tek başına değer üretir.
Bu kural olmadan, seed edilen 200 eksik ürün müşterilere açıklamasız/görselsiz olarak
satışa çıkar — kötü bir müşteri deneyimi ve yanlış katalog. Bu tek slice teslim edildiğinde
sistem tutarlı bir katalog sunar (eksikler saklı, tamlar satışta).

**Independent Test**: Katalog eksik ve tam ürünlerin karışımıyla doldurulur; müşteri
araması/listelemesi çağrılır; sonucun **yalnızca** tam (ve aktif) ürünleri içerdiği
doğrulanır. Eksik ürünler dışarıda kalır.

**Acceptance Scenarios**:

1. **Given** açıklaması boş veya görseli olmayan bir ürün, **When** müşteri o ürünü ada göre arar, **Then** ürün sonuçlarda görünmez.
2. **Given** hem açıklaması hem görseli dolu ve aktif bir ürün, **When** müşteri o ürünü ada göre arar, **Then** ürün sonuçlarda görünür.
3. **Given** açıklaması ve görseli dolu **ama** admin tarafından pasife alınmış (deaktif) bir ürün, **When** müşteri arar, **Then** ürün satışta görünmez (tamlık tek başına yetmez; aktiflik de gerekir).
4. **Given** tam ve aktif bir ürün, **When** açıklaması sonradan boşaltılır, **Then** ürün otomatik olarak satıştan düşer.

---

### User Story 2 - Bir ürün tamamlanınca satışa çıkar (Priority: P2)

Bir ürünün eksik açıklama ve görseli (bu feature'ın dışındaki herhangi bir yolla — agent,
admin elle veya içe aktarma) tamamlandığı anda, ürün ek bir manuel "yayına al" adımı
gerektirmeden satışa-hazır hale gelir ve (aktifse) müşteri aramalarında görünmeye başlar.

**Why this priority**: US1 kataloğu tutarlı kılar (eksikleri saklar); bu slice ise
tamamlanan ürünün **otomatik** olarak satışa dönmesini garanti eder — satılabilirliğin
ürünün durumundan türeyen bir kural olduğunu, ayrı bir bayrak yönetimi gerektirmediğini
doğrular. US1'in üstüne kurulur ve bağımsız test edilebilir.

**Independent Test**: Eksik bir ürün alınır; açıklama+görsel alanları doldurulur (test
içinde doğrudan); ürünün artık satışa-hazır sayıldığı ve (aktifse) müşteri aramasında
göründüğü doğrulanır.

**Acceptance Scenarios**:

1. **Given** açıklaması ve görseli eksik, aktif bir ürün, **When** açıklama ve görsel doldurulur, **Then** ürün müşteri aramalarında satışta görünür.
2. **Given** eksik bir ürün, **When** yalnızca açıklama doldurulur (görsel hâlâ boş), **Then** ürün eksik kalır ve satışta görünmez.

---

### User Story 3 - Operasyon eksik/tam envanteri görebilir (Priority: P3)

Bir operatör/admin, kataloğun sağlığını görmek için hangi ürünlerin eksik (satışa hazır
değil) ve hangilerinin tam olduğunu ayırt edebilir. Bu görünürlük, kaç ürünün hâlâ satış
dışı olduğunu izlemeyi sağlar.

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
- **Zaten satılmış eksik ürün yok**: Eksik ürün hiç satışa çıkmadığından, "eksik ama sepette/siparişte" durumu bu feature'ın kapsamı dışındadır.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem bir ürünü "tam/satışa hazır bilgiye sahip" saymalı **ancak ve ancak** açıklaması boş/whitespace değilse **VE** görseli null veya boş/whitespace değilse.
- **FR-002**: Sistem bir ürünü "satışta" (müşteriye satın alınabilir) saymalı **ancak ve ancak** ürün hem tam (FR-001) hem de aktif (admin tarafından etkinleştirilmiş) ise. Tamlık ve aktiflik ayrı kavramlardır ve biri diğerinin yerine geçmez.
- **FR-003**: Müşteriye ve müşteri adına çalışan asistan aramasına dönen ürün sonuçları, satışta **olmayan** (eksik ya da pasif) ürünleri içermemelidir.
- **FR-004**: Tamlık kuralı ürün aggregate'inin **içinde** bir invariant olarak korunmalı; ürünün satışa-hazır durumu, açıklama/görselin her değişiminde (oluşturma ve güncelleme dahil) tutarlı olmalıdır — bu kural çağıran katmanda (handler/endpoint) tekrar edilmemelidir.
- **FR-005**: Bir ürünün açıklaması ve görseli tamamlandığında, ürün (aktifse) ek bir manuel yayına-alma adımı gerektirmeden satışa-hazır duruma geçmelidir.
- **FR-006**: Operasyon/admin görünümü, satışta olmayan ürünleri gizlememeli; bunun yerine her ürünün satışa-hazır (tam) olup olmadığını ayırt edilebilir kılmalıdır.

### Key Entities *(include if feature involves data)*

- **Product (Ürün)**: Katalogdaki satılabilir kalem. Bu feature için ilgili nitelikleri: ad, **açıklama**, fiyat, SKU, marka, **görsel**, ve aktiflik durumu (admin aç/kapa). "Satışa hazır/tam" durumu bu niteliklerden (açıklama + görsel doluluğu) **türetilir**, ayrı bir yetki/alan değildir. "Satışta" = tam VE aktif.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Müşteri araması/listelemesi hiçbir koşulda eksik (açıklama veya görsel içermeyen) ürün döndürmez — eksik ürünlerin müşteriye görünürlüğü %0.
- **SC-002**: Bir ürünün açıklaması **ve** görseli tamamlandıktan sonra, ürün (aktifse) ek bir manuel yayına-alma adımı olmadan müşteri aramalarında satışta görünür.
- **SC-003**: Seed edilen 200 ürünün başlangıçta hiçbiri müşteri aramasında satışta görünmez (hepsi eksik).
- **SC-004**: Tam ama pasif (deaktif) bir ürün müşteri aramasında görünmez — yani tamlık, aktifliğin yerine geçmez (iki kavramın ayrılığı gözlemlenebilir).
- **SC-005**: Admin listelemesi hem eksik hem tam ürünleri içerir ve her ürünün satışa-hazır olup olmadığı ayırt edilebilir.

## Assumptions

- **Tamlık tanımı iki alanla sınırlı**: Yalnızca açıklama ve görsel doluluğu satışa-hazırlığı belirler; ad/fiyat/SKU zaten oluşturmada zorunlu kabul edilir ve bu feature'ın kapsamı dışındadır.
- **"Satışta" mevcut aktiflik kavramını korur**: Bugün ürünlerin bir aktiflik durumu (admin aç/kapa) vardır; bu feature onu değiştirmez, üzerine tamlık şartını ekler.
- **Müşteri arama akışı mevcut**: Müşteri/asistan araması bugün de aktiflik filtreliyor; bu feature filtreye tamlık şartını da ekler.
- **Eksikleri doldurma yolu bu feature dışı**: Ürünlerin nasıl tamamlandığı (agent, admin elle, içe aktarma) bu feature'ın konusu değildir; feature yalnızca satılabilirlik kuralını tanımlar.

## Out of Scope (ayrı feature)

- **Zenginleştirme agent'ı**: Eksik açıklama/görseli otomatik üreten AI agent'ı ayrı bir feature olarak spec'lenecektir. O feature yapay zeka ile **açıklama ve gerçek görsel** üretecek; tetikleme ve agent yerleşimi orada kararlaştırılacaktır. Bu feature (001) yalnızca, o ürünler tamamlandığında satışa çıkmalarını sağlayan kuralı garanti eder.