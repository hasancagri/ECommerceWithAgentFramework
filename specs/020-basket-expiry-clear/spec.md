# Feature Specification: Sepet Rezervasyon Süresi Dolunca Otomatik Boşaltma

**Feature Branch**: `020-basket-expiry-clear`

**Created**: 2026-07-28

**Status**: Done

**Input**: User description: "Sepet sayfasındaki süre sıfırlandığında sayfanın reload
olmasını ve sepetin boşaltılmasını istiyorum."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Süre bitince sepet gerçekten boşalır (Priority: P1)

Kullanıcı sepet sayfasında bekler; rezervasyon geri sayımı (017) sıfıra ulaşır.
O anda sepet sunucuda boşalır ve sayfa yenilenip boş sepet gösterir.
Süresi dolmuş ürünler ekranda kalmaz.

**Why this priority**: Tek istenen davranış bu. Bugün sayaç bitince yalnızca banner
"expired" olur; ürünler ekranda kalır; okuma temizlemediği için reload da geri getirir.

**Independent Test**: Sepete ürün ekle, geri sayım bitene kadar bekle; sayfanın
yenilendiğini ve "No items in the basket." gösterdiğini doğrula.

**Acceptance Scenarios**:

1. **Given** dolu sepet ve geri sayan sayaç, **When** sayaç sıfıra ulaşır,
   **Then** sepet sunucuda boşalır ve sayfa yenilenip boş sepet gösterir.
2. **Given** süresi dolmuş sepet, **When** kullanıcı sayfayı elle yeniler,
   **Then** boşaltma tetiklenir ve boş sepet görünür (expired ürün geri gelmez).
3. **Given** süresi DOLMAMIŞ sepet, **When** boşaltma tetiklenir,
   **Then** hiçbir şey silinmez (idempotent no-op) ve sayaç saymaya devam eder.

---

### Edge Cases

- Boşaltma çağrısı ağ hatasıyla başarısız olursa: sayfa yine de yenilenir;
  bir sonraki tetikleme (yenileme / yeni ekleme) tembel temizliği tekrar dener.
- Sayaç bitmeden sepet elle boşaltılırsa (son ürün silinir): sayaç/banner kaybolur;
  boşaltma tetiklenmez (gösterilecek sayaç yoktur).
- Aynı anda birden fazla boşaltma çağrısı gelirse: idempotent olduğundan zararsızdır.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sepet sayfası, rezervasyon geri sayımı sıfıra ulaştığında bir boşaltma
  eylemi tetiklemek ZORUNDADIR (bugünkü yalnızca-banner davranışı yerine).
- **FR-002**: Sistem, süresi dolmuş bir sepetin TÜM satırlarını sunucuda silip
  rezervasyon çapasını sıfırlayan bir işlem SUNMALIDIR (gerçek boşaltma, UI gizleme değil).
- **FR-003**: Bu boşaltma işlemi idempotent OLMALIDIR — süre dolmamışsa hiçbir şey
  değiştirmez (no-op); tekrarlı çağrı ek etki yaratmaz.
- **FR-004**: Boşaltma tetiklendikten sonra sepet sayfası yenilenmeli ve boş sepeti
  ("No items in the basket.") göstermeli, süresi dolmuş ürünleri GÖSTERMEMELİDİR.
- **FR-005**: Boşaltma yalnızca sepet sahibi kullanıcının kendi sepetini etkilemeli;
  yetkilendirme mevcut sepet-yazma kuralına uymalıdır.
- **FR-006**: Boşaltma, süresi zaten dolmuş rezervasyonlar için stok bırakma (Release)
  ÇAĞIRMAMALIDIR — rezervasyonlar aynı mutlak anda dolar, sweep süpürür (017 deseni).
- **FR-007**: Kapsam yalnızca sepet sayfasıdır; ödeme/sipariş akışı DEĞİŞMEZ.

### Key Entities *(include if feature involves data)*

- **Sepet (Basket)**: Kullanıcının rezerve ürünlerini ve tek mutlak rezervasyon
  bitişini (çapa) taşır. Süre dolunca satırları düşürülür, çapası sıfırlanır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Sayaç sıfıra ulaştıktan sonra kullanıcı, süresi dolmuş hiçbir ürünü
  sepette GÖREMEZ (sayfa boş sepet gösterir).
- **SC-002**: Süresi dolmuş sepette sayfa yenilendiğinde sepet %100 boş döner
  (expired ürünler asla geri gelmez).
- **SC-003**: Süresi dolmamış sepette boşaltma tetiklense bile sepet aynen korunur
  (yanlış-pozitif silme = 0).

## Assumptions

- Rezervasyon bitiş anı sepet modelindeki mevcut çapa (ReservationExpiresAt) ile
  belirlenir; yeni bir zaman kaynağı eklenmez (017 temeli korunur).
- Boşaltma davranışı Basket aggregate'inde zaten mevcut olan tembel temizlik
  mantığını yeniden kullanır; yeni domain kuralı icat edilmez.
- Sunucu tarafı süreli-temizlik (Hangfire sweep, 017) yürürlükte kalır; bu feature
  onu değiştirmeden, kullanıcı-tetikli anlık bir boşaltma yolu ekler.
- İstemci ile sunucu saati küçük farklarla kayabilir; boşaltma idempotent no-op
  olduğundan erken/gecikmiş tetikleme güvenlidir.