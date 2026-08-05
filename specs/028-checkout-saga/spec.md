# Feature Specification: Checkout Saga (Orchestration)

**Feature Branch**: `028-checkout-saga`

**Created**: 2026-08-05

**Status**: Draft

**Input**: User description: "Sipariş oluşturma akışını Wolverine durable saga ile orkestre et; stok commit + telafi (RevertCommit),
sipariş Confirm/Cancel, sepet temizliği saga adımı; OrderCreatedEvent silinir; Payment kapsam dışı."

**Kademe**: Tam — yeni gRPC kontratları (RevertCommit, ClearBasket), servisler-arası davranış değişikliği, event silme.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Sipariş asenkron oluşur ve onaylanır (Priority: P1)

Müşteri checkout'u tamamlar; sipariş "Beklemede" doğar ve ekran hemen döner.
Arka planda stok kalıcı düşülür, sipariş "Onaylandı" olur, sepet boşalır.

**Why this priority**: Ana satın alma yolu; saga'nın mutlu yolu. Bu olmadan feature yok.

**Independent Test**: Stoklu ürünle checkout; Siparişlerim'de rozetin Beklemede→Onaylandı olduğu ve sepetin boşaldığı görülür.

**Acceptance Scenarios**:

1. **Given** rezervasyonlu sepet, **When** checkout, **Then** yanıt hemen döner ve sipariş "Beklemede" listelenir.
2. **Given** "Beklemede" sipariş, **When** süreç tamamlanır, **Then** sipariş "Onaylandı" olur ve stok kalıcı düşmüştür.
3. **Given** onaylanan sipariş, **When** kullanıcı sepete bakar, **Then** sepet boştur.

---

### User Story 2 - Stok düşülemezse sipariş iptal ve stok telafisi (Priority: P1)

Bir kalemin stok commit'i başarısız olursa sipariş iptal edilir; o ana dek commit edilen kalemler stoğa geri eklenir.

**Why this priority**: Saga'nın varlık sebebi; bugünkü partial-commit tutarsızlığını kapatır.

**Independent Test**: İki kalemli sepette 2. kalemin stoğu düşürülemez yapılır; sipariş "İptal" olur, 1. kalemin stoğu geri gelir.

**Acceptance Scenarios**:

1. **Given** 2 kalemli sipariş ve 2. kalemde yetersiz stok, **When** saga koşar, **Then** sipariş "İptal" olur ve sebep görünür.
2. **Given** aynı senaryo, **When** telafi biter, **Then** 1. kalemin commit'i geri alınmıştır (OnHand eski değerinde).
3. **Given** iptal edilen sipariş, **When** kullanıcı sepete bakar, **Then** sepet DURUR (temizlenmez), tekrar deneyebilir.

---

### User Story 3 - Takılan süreç watchdog ile kapanır (Priority: P2)

Süreç yarıda asılı kalırsa (servis çökmesi, kayıp mesaj) watchdog süresi dolunca telafi çalışır ve sipariş iptal edilir.

**Why this priority**: Sonsuza dek "Beklemede" kalan sipariş olamaz; güvenlik ağı.

**Independent Test**: Stock servisi kapatılıp checkout yapılır; watchdog süresi sonunda sipariş "İptal (zaman aşımı)" olur.

**Acceptance Scenarios**:

1. **Given** Stock erişilemez, **When** retry'lar tükenir veya watchdog dolar, **Then** sipariş "İptal" ve sebep zaman aşımı/stok hatasıdır.
2. **Given** watchdog dolmadan süreç bitti, **When** watchdog mesajı gelir, **Then** hiçbir etki olmaz (no-op).

---

### User Story 4 - Sepet temizliği siparişi asla düşürmez (Priority: P3)

Sipariş onaylandıktan sonra sepet temizliği başarısız olsa bile sipariş "Onaylandı" kalır; temizlik sınırlı tekrar dener.

**Why this priority**: Pivot-sonrası adım; müşteri parası/siparişi UI pürüzüne feda edilemez.

**Acceptance Scenarios**:

1. **Given** onaylanmış sipariş ve Basket erişilemez, **When** retry tükenir, **Then** sipariş "Onaylandı" kalır, durum loglanır.

---

### Edge Cases

- Aynı PaymentId ile ikinci checkout: mevcut idempotency korunur, istek reddedilir.
- Telafi (RevertCommit) de başarısız olursa: saga "telafi başarısız" durumuna düşer, alarm loglanır; sipariş yine "İptal" işaretlenir.
- Aynı kalem için telafinin iki kez tetiklenmesi: RevertCommit idempotenttir; stok bir defadan fazla artmaz.
- Sepeti olmayan kullanıcı için ClearBasket: başarılı sayılır (idempotent, no-op).
- Watchdog mesajı, saga bittikten sonra gelirse: etkisizdir.
- Kullanıcı "Beklemede" sipariş dururken tekrar checkout yaparsa: yeni PaymentId ile yeni sipariş açılabilir; stok kuralları yine korur.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Checkout, siparişi "Beklemede" durumunda yaratmalı ve yanıtı süreç sonunu beklemeden dönmelidir.
- **FR-002**: Sipariş yaşam döngüsü Beklemede→Onaylandı ve Beklemede→İptal geçişleriyle sınırlıdır; başka geçiş reddedilir.
- **FR-003**: İptal edilen sipariş sebep taşımalıdır; sebep resource kodu olarak saklanır, UI'da metin gösterilir.
- **FR-004**: Süreç her kalem için stoğu kalıcı düşürmeli (Commit); kalemler sırayla işlenmelidir.
- **FR-005**: İş hatasında (yetersiz stok, rezervasyon yok) tekrar denenmez; teknik hatada sınırlı tekrar denenir.
- **FR-006**: Herhangi bir kalem commit edilemezse, o ana dek commit edilen tüm kalemler stoğa geri eklenmelidir (telafi).
- **FR-007**: Telafi operasyonu (stok geri ekleme) idempotent olmalıdır; mükerrer çağrı stoku birden fazla artırmaz.
- **FR-008**: Tüm kalemler düşülünce sipariş "Onaylandı" olmalı; sonrasında kullanıcının sepeti temizlenmelidir.
- **FR-009**: Sepet temizliği pivot-sonrası adımdır: başarısızlığı siparişi etkilemez; sınırlı tekrar + log ile bırakılır.
- **FR-010**: Sepet temizliği idempotenttir; sepet yoksa başarılı sayılır.
- **FR-011**: Süreç, yapılandırılabilir bir watchdog süresi (varsayılan 2 dk) içinde bitmezse telafi + iptal uygulanmalıdır.
- **FR-012**: Watchdog, süreç bittikten sonra tetiklenirse hiçbir etki üretmemelidir.
- **FR-013**: Telafi de başarısız olursa süreç "telafi başarısız" olarak işaretlenir ve alarm düzeyinde loglanır.
- **FR-014**: Süreç durumu kalıcıdır; servis yeniden başlasa da yarım süreç kaldığı yerden ele alınabilir.
- **FR-015**: `OrderCreatedEvent` ve Basket'teki tüketicisi kaldırılır; sepet temizliği yalnız saga adımıyla yapılır.
- **FR-016**: Sipariş oluşturmadaki PaymentId idempotency'si aynen korunur.
- **FR-017**: Servisler-arası saga adımları tipli senkron kontratlarla yapılır; çağrılar kullanıcı kimliğiyle yetkilendirilir.
- **FR-018**: WebApp checkout sonrası Siparişlerim'e yönlendirir; durum rozeti Beklemede/Onaylandı/İptal (+sebep) gösterir.
- **FR-019**: Sipariş listesi/detayı yeni durumları gösterir; otomatik yenileme (polling) yoktur.

### Key Entities

- **Order (Sipariş)**: Durum makinesi kazanır — Beklemede/Onaylandı/İptal + iptal sebebi. Geçiş kuralları aggregate içinde korunur.
- **Checkout Süreci (saga durumu)**: Sipariş kimliğiyle anahtarlanan kalıcı süreç kaydı; kalemler ve commit edilenler listesini taşır.
- **Stok kalemi**: Commit ile kalıcı düşer; telafi ile geri artar. Otorite Stock context'indedir.
- **Sepet**: Onay sonrası temizlenen kullanıcı sepeti; süreç açısından pivot-sonrası bağımlılık.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Checkout yanıtı, stok işlemleri bitmeden döner; kullanıcı 1 sn içinde sipariş kaydını "Beklemede" görür.
- **SC-002**: Mutlu yolda sipariş 10 sn içinde "Onaylandı" olur ve sepet boşalır (normalde saniyeler içinde).
- **SC-003**: Kısmi stok hatasında hiçbir kalıcı stok kaybı kalmaz: commit edilen kalemler %100 geri eklenir.
- **SC-004**: Hiçbir sipariş watchdog süresinden (varsayılan 2 dk) uzun "Beklemede" kalmaz.
- **SC-005**: Sepet temizliği hatası hiçbir siparişi iptal ettirmez (0 vaka).
- **SC-006**: Aynı telafinin mükerrer uygulanması stok toplamını değiştirmez (idempotency kanıtı).

## Assumptions

- Ödeme kapsam DIŞI: maket PaymentId girdisi ve idempotency aynen sürer; Payment feature'ı gelince saga'ya adım eklenir.
- Bilgilendirme e-postası Payment feature'ına ertelendi (kullanıcı notu, 2026-08-05).
- Saga altyapısı Wolverine durable saga + Marten persistence'tır; watchdog Wolverine scheduled message ile kurulur.
- Stok telafisi için stok kontratına RevertCommit; sepet temizliği için yeni ClearBasket senkron kontratı eklenir.
- Anayasa İlke I amendment gerekebilir: gRPC sanksiyonu "anlık karar" ifadesiyle sınırlı; saga adım komutları bunu genişletir.
- Mevcut "Paid" sipariş durumu "Onaylandı" (Confirmed) olarak evrilir; tarihi kayıtlar migration'la eşlenir veya DB sıfırdan gelir.
- Rezervasyon TTL mekanizması (012/026) değişmez; saga yalnız checkout sürecini kapsar.
- İptal edilen siparişte sepet korunur; kullanıcı düzeltip yeniden checkout yapabilir.