# Feature Specification: Product Tamlık (IsComplete) Kuralının Kaldırılması

**Feature Branch**: `010-remove-product-iscomplete`

**Created**: 2026-07-24

**Status**: Draft

**Input**: User description: "IsComplete property'sini kaldır" — görsel/açıklama tamlık kuralı ve
ona bağlı arama filtreleri söküllsün; ürünler tamlık şartı olmadan bulunur/satılır olsun.

**Kademe**: Küçük — tek aggregate (Product), tablo/kontrat/event değişikliği yok; belirsizlik yok.

**Bağlam**: 001'in "eksik ürün satışta olamaz" invariant'ı bilinçli geri alınıyor (kullanıcı kararı,
2026-07-24). Neden: tedarikçi feed'i görsel taşımıyor → 200 ürünün tamamı IsComplete=false →
agent araması hiçbir ingestion ürününü bulamıyor; görsel zenginleştirme yoluna girilmeyecek.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Görselsiz ürün de bulunur (Priority: P1)

Müşteri olarak chat'e "Adidas Model 119 kaç adet var" diye sorduğumda, ürünün görseli olmasa da
asistanın ürünü bulmasını istiyorum.

**Why this priority**: Bildirilen hatanın kendisi; kaldırmanın tek amacı bu.

**Independent Test**: Ingestion'dan gelen (görselsiz) bir ürün chat/agent aramasıyla bulunur.

**Acceptance Scenarios**:

1. **Given** görselsiz aktif ürün, **When** adıyla aranır (agent tool'u), **Then** ürün bulunur.
2. **Given** IsActive=false ürün, **When** aranır, **Then** yine bulunmaz (aktiflik kuralı yaşar).

---

### Edge Cases

- Eski dokümanlardaki kalıcı `IsComplete` alanı: model alanı silinince Newtonsoft bilinmeyen alanı
  yok sayar; migration gerekmez (doğrulanır).
- `IsOnSale` türetilmişi tamamen kalkar — hiçbir tüketicisi yok (WebApp/Storefront taramasıyla kanıtlı).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `Product` aggregate'inden `IsComplete`, `IsOnSale` ve tamlık yeniden hesaplama kuralı kalkar.
- **FR-002**: Arama/okuma filtrelerinden tamlık şartı kalkar (`SearchProducts`, `GetProduct`,
  `GetProductByName`); `IsDeleted`/`IsActive` şartları aynen korunur.
- **FR-003**: `GetAllProducts` yanıtından `IsComplete`/`IsOnSale` alanları kalkar (tüketicisi yok).
- **FR-004**: Tamlık birim testleri kaldırılır; aktiflik davranışı test edilmeye devam eder.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Canlıda chat/agent araması "Adidas Model 119"u bulur (bugün: bulamıyor).
- **SC-002**: Tüm çözüm derlenir ve testler geçer; tamlığa dair hiçbir referans kalmaz.

## Assumptions

- 001 spec'i tarihsel kayıt olarak kalır; bu feature onun bilinçli geri alınışıdır (roles emsali).
- Görsel akışı (`todo-catalog-picture-handler`) ayrı bir iştir; bu feature onu çözmez, beklemez.