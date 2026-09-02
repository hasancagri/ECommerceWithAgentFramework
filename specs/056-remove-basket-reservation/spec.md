# Feature Specification: Sepet Rezervasyonu ve Süre Sisteminin Sökümü (Kalıcı Sepet)

**Feature Branch**: `056-remove-basket-reservation`

**Created**: 2026-09-02

**Status**: Draft

**Input**: User description: "Sepete ürün atıldığında sürenin işlemesini kaldır — kitapyurdu modeli: sepet stok tutmaz, süre yok; stok gerçeğinin tek anı checkout."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sepet kalıcıdır, süre yoktur (Priority: P1)

Ali bir kitabı sepete atar. Hiçbir sayaç başlamaz, kitap onun için stokta AYRILMAZ. Ali üç gün
sonra döndüğünde sepeti aynen durur; kimse sepetini boşaltmamıştır.

**Why this priority**: Feature'ın çekirdeği — kullanıcıya görünen davranış değişikliği bu.

**Independent Test**: Sepete ürün ekle; sayaç görünmediğini, sürenin hiçbir yerde başlamadığını ve
sepetin süre geçse de dolu kaldığını doğrula.

**Acceptance Scenarios**:

1. **Given** boş sepet, **When** ürün eklenir, **Then** hiçbir yerde geri sayım görünmez ve sepete süre damgası yazılmaz.
2. **Given** dolu sepet, **When** üzerinden uzun süre (saatler/günler) geçer, **Then** sepet içeriği aynen durur; otomatik boşalma olmaz.
3. **Given** dolu sepet, **When** ürün eklenir/çıkarılır, **Then** stok tarafında hiçbir ayırma (rezervasyon) oluşmaz/serbest bırakılmaz.

---

### User Story 2 - Stok gerçeği checkout anında (Priority: P1)

Ayşe sepetindeki 2 kitap için ödemeye geçer. Sistem stok düşümünü ödeme ÖNCESİNDE, checkout
sürecinin içinde yapar: stok yeterliyse süreç ödemeye ilerler; yetersizse sipariş iptal olur,
ödeme alınmaz ve Ayşe net bir "stok yetersiz" sonucu görür.

**Why this priority**: Rezervasyon kalkınca stoğun tek koruyucusu checkout düşümüdür; bu adım
yanlışsa fazla satış (oversell) olur.

**Independent Test**: Stok X iken X adetlik checkout başarılı; X+1 adetlik checkout sipariş
oluşturmadan/iptal ederek sonlanır, ödeme çekilmez.

**Acceptance Scenarios**:

1. **Given** stokta 3 adet, **When** 2 adetlik checkout tamamlanır, **Then** sipariş oluşur ve stok 1'e düşer.
2. **Given** stokta 1 adet, **When** 2 adetlik checkout başlar, **Then** stok düşümü başarısız olur, süreç ödeme almadan siparişi iptal eder, kullanıcı stok yetersizliğini görür.
3. **Given** çok kalemli sepette bir kalemin stoğu yetersiz, **When** checkout başlar, **Then** önceden düşülmüş kalemler geri alınır (telafi) ve sipariş iptal olur.

---

### User Story 3 - Son ürün yarışı (Priority: P2)

Son 1 adet kalan kitabı hem Ali hem Ayşe sepetine koyar — ikisi de koyabilir, kimseye söz
verilmez. İlk ödemeyi tamamlayan kitabı alır; ikincisinin checkout'u stok yetersizliğiyle iptal
olur.

**Why this priority**: Rezervasyonsuz modelin bilinçli trade-off'u; davranışın belirsiz değil
tanımlı olması gerekir.

**Independent Test**: İki kullanıcı aynı son ürünü sepete ekler; ikisi de ekleyebilmeli, yalnız
ilk checkout başarılı olmalı.

**Acceptance Scenarios**:

1. **Given** stokta 1 adet, **When** iki kullanıcı da ürünü sepete ekler, **Then** ikisinin sepetine de girer (engel yok).
2. **Given** ikisi de checkout başlatır, **When** ilki stok düşümünü tamamlar, **Then** ikincinin checkout'u iptal olur ve ödemesi alınmaz.

---

### Edge Cases

- Eski (söküm öncesi) sepet kayıtlarında süre damgası alanı dolu — okuma kırılmamalı, alan yok sayılmalı.
- Söküm anında kuyruğa zamanlanmış süre-bitti temizlik mesajları uçuşta olabilir — güvenle boşa düşmeli, hata üretmemeli.
- Sepette beklerken ürünün stoğu tükenirse sepet DOKUNULMAZ; gerçek checkout'ta ortaya çıkar.
- Miktar artırmada adet tavanı (5) sürer; stok-bazlı ek sınır uygulanmaz (stok gerçeği checkout'ta).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sepete ürün ekleme/çıkarma/adet değiştirme stok tarafında HİÇBİR ayırma yapmaz ve stok servisine bu amaçla çağrı gitmez.
- **FR-002**: Sepette süre kavramı tamamen kalkar: süre damgası tutulmaz, geri sayım gösterilmez, süre-bitti temizliği (arka plan + kullanıcı tetikli) çalışmaz.
- **FR-003**: Sepet yalnız kullanıcı eylemiyle (silme/adet) ya da başarılı checkout sonunda değişir; zamana bağlı otomatik boşalma yoktur.
- **FR-004**: Checkout stok düşümü tek gerçek andır: yetersizse adım başarısız olur, ödeme alınmadan sipariş iptal edilir ve önceden düşülen kalemler geri alınır (mevcut telafi düzeni).
- **FR-005**: Sepette kalem başına adet tavanı (5) aynen sürer.
- **FR-006**: Rezervasyon kavramı iki domain'den de silinir: sepet tarafında süre çapası/başlatma/temizleme davranışları; stok tarafında ayırma kayıtları, ayırma/serbest bırakma uçları, süre süpürme zamanlaması ve "rezervasyon süresi doldu" olayı.
- **FR-007**: Rezervasyona özel yetki kapsamı (scope) kapalı registry'den kaldırılır.
- **FR-008**: Basket, Stock ve Checkout süreç belgeleri (FLOW.md) aynı değişiklik setinde güncellenir (İLKE VII).
- **FR-009**: Eski sepet/stok kayıtları (dolu süre alanı, artık ayırma listesi) okunurken sistem kırılmaz; kalıntı alanlar yok sayılır.
- **FR-010**: Checkout stok düşümünün tekrar teslim güvenliği (aynı sipariş için idempotency) ve geri alma yolu korunur.

### Key Entities

- **Sepet (Basket)**: Kullanıcının kalıcı alışveriş listesi; süre çapası kalkar, yalnız kalemler + adetler kalır.
- **Ürün Stoğu (ProductStock)**: Eldeki miktar (OnHand) tek gerçek; ayırma (reservation) listesi kalkar; düşüm/geri alma checkout sürecinden gelir.
- **Checkout Süreci (CheckoutProcess)**: Değişmez akış: sipariş → stok düşümü → ödeme → onay → sepet temizliği; stok düşümü artık ayırma-çevirme değil doğrudan düşümdür.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Sepete eklenen ürün, aradan geçen süreden bağımsız (24 saat+) sepette kalır; otomatik boşalma sıfır vakadır.
- **SC-002**: Uygulamanın hiçbir sayfasında sepet geri sayımı görünmez.
- **SC-003**: Stok X iken toplamı X'i aşan checkout denemesi ödeme alınmadan iptal olur; stok eksiye düşmez (oversell sıfır).
- **SC-004**: Son ürün yarışında yalnız ilk tamamlanan checkout sipariş üretir; ikincisi net hata sonucu görür.
- **SC-005**: Tüm mevcut testler yeşil kalır; rezervasyona özel testler kaldırılır, checkout-düşüm yolu için eşdeğer kapsam eklenir.

## Assumptions

- Sepete eklerken stok ön-kontrolü YAPILMAZ (kitapyurdu modeli); ürün sayfasındaki stok görünürlüğü kullanıcıya yeterli sinyaldir. Adet tavanı 5 makul üst sınır olarak kalır.
- Checkout sürecinin mevcut adım sırası, telafi (LIFO) düzeni, watchdog ve idempotency mekanizması değişmez; yalnız stok adımının iç anlamı değişir.
- Başarılı checkout sonrası sepet temizliği (mevcut akış) aynen kalır.
- Dev ortam tek adımlı geçiş: eski kayıtlardaki kalıntı alanlar dokümana dayalı depolamada tolere edilir, ayrı veri taşıma çalışması gerekmez.