# Feature Specification: Sepet Düzeyi Tek Rezervasyon Süresi (Basket Reservation Anchor)

**Feature Branch**: `017-basket-reservation-anchor`

**Created**: 2026-07-28

**Status**: Draft

**Input**: User description: "Sepetin tek bir rezervasyon süresi olsun; ilk ürünle başlar,
ekleme/çıkarma değiştirmez, sepet boşalınca sıfırlanır; süre gerçektir (Stock rezervasyonları
aynı anda dolar, mevcut sweep/event zinciri temizler); UI'da satır sayaçları yerine tek banner."

**Kademe**: Tam — paylaşılan rezervasyon kontratı değişir ve iki bounded context (Basket, Stock) etkilenir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tek sepet sayacı (Priority: P1)

Müşteri boş sepetine ilk ürünü ekler; o anda sepetin TEK rezervasyon süresi başlar.

Sepet sayfasında satır bazında sayaç yoktur; tablonun üstünde tek bir geri sayım görünür.

**Why this priority**: Özelliğin görünür değeri budur; bugünkü satır-başına sayaç kafa karıştırıyor.

**Independent Test**: Boş sepete ürün ekle, sepet sayfasını aç; tek banner'da süre geri saydığı görülür.

**Acceptance Scenarios**:

1. **Given** boş sepet, **When** ilk ürün eklenir, **Then** sepet süresi tam süreden başlar.
2. **Given** süreli sepet, **When** sepet sayfası açılır, **Then** tek geri sayım banner'ı görünür.
3. **Given** süreli sepet, **When** satırlara bakılır, **Then** satır bazında sayaç/rezervasyon sütunu yoktur.

---

### User Story 2 - Süre gerçektir: topluca dolma ve temizlik (Priority: P1)

Sepet süresi dolduğunda sepetteki TÜM ürünlerin stok rezervasyonları aynı anda dolar.

Mevcut periyodik temizlik ve olay zinciri değişmeden çalışır; sepet satırları otomatik düşer.

**Why this priority**: Sayaç salt görsel olamaz; kullanıcıya gösterilen süre stok gerçeğiyle aynı olmalı.

**Independent Test**: Kısa süre config'le sepete 2+ ürün ekle; süre bitince tüm satırların düştüğü görülür.

**Acceptance Scenarios**:

1. **Given** 2+ ürünlü süreli sepet, **When** süre dolar, **Then** tüm rezervasyonlar aynı anda dolmuş sayılır.
2. **Given** süresi dolan sepet, **When** periyodik temizlik koşar, **Then** tüm sepet satırları otomatik silinir.
3. **Given** süresi dolan sepet, **When** temizlik henüz koşmadıysa, **Then** banner "Expired" gösterir.

---

### User Story 3 - Çapa sabitliği (Priority: P2)

Sepete sonradan eklenen ürünler süreyi değiştirmez; hepsi mevcut sepet süresine bağlanır.

Süreyi başlatan ürün sepetten çıkarılsa bile sayaç işlemeye devam eder.

**Why this priority**: Tek sayacın tutarlılık kuralı; olmadan süre zıplar ve güven kaybolur.

**Independent Test**: İlk ürünü ekle, süreyi not et; ikinci ürünü ekle ve ilkini sil; süre değişmemelidir.

**Acceptance Scenarios**:

1. **Given** süreli sepet, **When** yeni ürün eklenir, **Then** sepet süresi değişmez.
2. **Given** süreli sepet, **When** yeni eklenen ürünün rezervasyonu yapılır, **Then** bitişi sepet süresiyle aynıdır.
3. **Given** iki ürünlü sepet, **When** süreyi başlatan ürün silinir, **Then** sayaç kalan süreden devam eder.
4. **Given** süreli sepet, **When** bir ürünün adedi değişir, **Then** sepet süresi değişmez.

---

### User Story 4 - Sıfırlama ve yeniden başlama (Priority: P3)

Sepet tamamen boşalınca (elle silme, sipariş, süre dolumu) sepet süresi sıfırlanır.

Sonraki ilk ürün ekleme yeni ve tam bir süre başlatır.

**Why this priority**: Yaşam döngüsünü kapatır; olmadan eski süre yeni sepete taşınır.

**Independent Test**: Sepeti boşalt, yeni ürün ekle; sayacın tam süreden yeniden başladığı görülür.

**Acceptance Scenarios**:

1. **Given** süreli sepet, **When** son ürün silinir, **Then** sepet süresi sıfırlanır (banner çıkmaz).
2. **Given** boşalmış sepet, **When** yeni ürün eklenir, **Then** yeni süre tam süreden başlar.
3. **Given** sipariş verilen sepet, **When** sepet temizlenir, **Then** sepet süresi de sıfırlanır.

---

### Edge Cases

- Süre doldu ama periyodik temizlik henüz koşmadı: banner "Expired" gösterir; satırlar en geç bir sonraki temizlikte düşer.
- Süresi dolmuş (henüz temizlenmemiş) sepete ekleme: önce dolmuş satırlar düşürülür, sonra boş sepet gibi yeni süre başlar.
- Özellik öncesi kalan sepetler (sepet süresi yok, satırlar var): banner çıkmaz; ilk yeni ekleme sepet süresini kurar.
- İlk eklemede stok reddi (yetersiz/erişilemez): sepete yazılmaz, süre başlatılmaz (mevcut fail-closed korunur).
- Rezervasyon çağrısında süre alanı verilmezse: bugünkü sabit-TTL davranışı geçerlidir (Order akışı etkilenmez).
- Tüm zamanlar UTC mutlak zamandır; istemci saati yalnız gösterim içindir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sepet, sepet düzeyinde tek bir rezervasyon bitiş zamanı (çapa) taşımalıdır.
- **FR-002**: Çapa yokken yapılan ilk başarılı ürün ekleme çapayı "şimdi + yapılandırılabilir süre" olarak kurmalıdır.
- **FR-003**: Çapa varken ekleme, adet değişikliği ve tekil silme çapayı DEĞİŞTİRMEMELİDİR.
- **FR-004**: Sepet tamamen boşaldığında çapa sıfırlanmalıdır; sonraki ilk ekleme yeni çapa kurmalıdır.
- **FR-005**: Sepetteki her stok rezervasyonu, çapanın mutlak bitiş zamanıyla oluşturulmalıdır.
- **FR-006**: Rezervasyon kontratı opsiyonel mutlak bitiş zamanı kabul etmelidir; verilmezse mevcut sabit TTL uygulanır.
- **FR-007**: Süre dolumunda mevcut periyodik temizlik + rezervasyon-doldu olay zinciri değişmeden çalışmalıdır.
- **FR-008**: Süresi dolmuş sepete yeni ekleme, önce dolmuş satırları düşürmeli, sonra yeni çapa kurmalıdır.
- **FR-009**: Sepet sorgusu sepet düzeyi bitiş zamanını döndürmelidir; satır bazında bitiş UI'da kullanılmaz.
- **FR-010**: Sepet, "süresi dolmuş" bilgisini türetilmiş olarak sunmalıdır (ürün varsa ve çapa geçmişse).
- **FR-011**: Sepet sayfasında satır sayaçları kaldırılmalı; tablonun üstünde tek geri sayım banner'ı gösterilmelidir.
- **FR-012**: Banner süre dolunca "Expired" durumuna geçmelidir; ödeme adımına geçiş bloklanmaz.
- **FR-013**: Sepet süresi servis yapılandırmasından okunmalıdır (varsayılan 5 dakika).
- **FR-014**: Sipariş akışının rezervasyon kesinleştirme (commit) davranışı değişmemelidir.

### Key Entities

- **Sepet**: Kullanıcının sepeti; satırlara ek olarak sepet düzeyi rezervasyon bitiş zamanı (çapa) taşır.
- **Stok Rezervasyonu**: (ürün, kullanıcı) başına ayrılan adet + mutlak bitiş; artık bitişi sepet çapasından alabilir.
- **Rezervasyon Kontratı**: Sepet→Stok senkron çağrısı; opsiyonel mutlak bitiş alanı eklenir (geriye uyumlu).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Sepet sayfasında tam olarak bir geri sayım görünür; satır bazında sayaç sayısı sıfırdır.
- **SC-002**: İlk eklemeden sonra yapılan 3 ekleme/silme işleminin hiçbiri gösterilen süreyi değiştirmez.
- **SC-003**: Süre dolumundan sonra en geç 2 dakika içinde sepetteki tüm satırlar otomatik düşer.
- **SC-004**: Sepet boşaldıktan sonraki ilk ekleme, sürenin tam yapılandırılmış değerden başladığını gösterir.
- **SC-005**: Sipariş verme akışı (rezervasyon kesinleştirme dahil) davranış değişikliği olmadan tamamlanır.

## Assumptions

- Sepet süresi varsayılanı 5 dakikadır; servis yapılandırmasıyla değiştirilebilir (test için kısaltılabilir).
- Ürün-başına sabit TTL mekanizması kontratta korunur; süre alanı verilmeyen çağrılar için geçerli kalır.
- Sipariş (Order) akışı rezervasyon süresi geçirmez; mevcut davranışı sürer.
- Mevcut sepet verisi için migration yapılmaz; eski sepetler ilk yeni eklemede çapa kazanır.
- Periyodik temizlik sıklığı (dakikalık) yeterlidir; SC-003'teki 2 dakika bu sıklığı hesaba katar.
- Süre dolumu ödemeyi bloklamaz; yarış durumunda stok kesinleştirme mevcut kurallarla sonuçlanır.