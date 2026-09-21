# Feature Specification: Kampanya İndirim Motoru (Discount.Api)

**Feature Branch**: `079-discount-campaigns`

**Created**: 2026-09-21

**Status**: Draft

**Input**: Yeni Discount.Api BC — admin-güdümlü kampanya indirimi. Admin metinle "şu kategoriye/yazara/
yayınevine/kitaba, şu tarihe kadar, %şu indirim" der; sistem süzgeci kitap setine çözer, her kitaba
indirim işler. **Kitap başına EN FAZLA 1 ETKİN indirim** (son-gelen-kazanır: varsa üzerine yazılır). İndirim vitrine itilir, listede inline
görünür; checkout doğrular. Kupon bu sürümde YOK. Katalog import'undan bağımsız.

## Clarifications

### Session 2026-09-21

- **Kitap başına en fazla 1 ETKİN indirim** (temel değişmez). Overlap/en-iyi-kazanır motoru YOK — çakışma
  politikası **son-gelen-kazanır**: yeni kampanya kitabın mevcut indirimini **ezer** (üzerine yazar; atlama
  YOK). Kayıt ait olduğu kampanyayla (CampaignId) yaşar; sonradan başka kampanya ezerse eski kampanyanın
  bitişi bu kaydı temizlemez. ProductDiscount yalnız aktifleşmede yazılır (Scheduled kampanya slot tutmaz).
  (Rev 2026-09-21: eski "varsa atla / ilk-gelen-kazanır" kararı İPTAL — kullanıcı kararı.)
- Admin indirimi bir **süzgeçle** açar: kategori · yazar · yayınevi · tek-kitap. Süzgeç **uygulama anında**
  somut kitap listesine çözülür (**snapshot** — sonradan eklenen kitap otomatik girmez; canlı boyut değil).
- İndirim değeri = **yüzde** (sabit tutar ertelendi). İndirim NEREDE: yüzde **vitrine event'le itilir**
  (herkese-aynı → read-model'e oturur); son para **checkout'ta Discount.Api gRPC ile canlı doğrulanır**.
- Discount.Api **fiyat TUTMAZ** — saf yüzde otoritesi; etkin fiyatı tüketici (vitrin/checkout) kendi liste
  fiyatından hesaplar. Liste fiyatı değişince vitrin otomatik doğru hesaplar (ekstra push yok).
- Süre yönetimi = **per-kampanya Wolverine scheduled message** (`ScheduleAsync`): `startsAt`→aktifle,
  `endsAt`→o kampanyanın kitaplarını temizle. Fire **guard'lı idempotent** (bayat mesaj no-op; edit/cancel'da
  reschedule zorunlu değil). **View-guard yedek**: fire gecikirse `now` pencere dışıysa vitrin indirimi gizler.
- Sepete-ekleme anı indirimi **KİLİTLEMEZ** (grace yok): sepetteyken kampanya biterse checkout indirim
  uygulamaz, müşteri liste fiyatını öder. İndirim = ödeme anında geçerli olan.
- Excel katalog import'u indirim TAŞIMAZ (D14 düştü) — kampanya saf admin işi; 080'den bağımsız.
- Kapsam dışı bırakılanlar: kupon (G6.1), sabit-tutar, **en-iyi-kazanır/dinamik fiyat kuralı** (D16 yön),
  değişen-kitaplar rapor maili (D16 tech-debt), AdminActionLog (D4'te söküldü).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin süzgeçle indirim açar, kitaplar listede indirimli görünür (Priority: P1)

Admin `/mcp-admin`'den "Roman kategorisine 30 Eylül'e kadar %20 indirim" (ya da yazar/yayınevi/tek-kitap)
der. Sistem süzgeci kitap setine çözer, her kitaba indirim işler (önceki indirimi varsa üzerine yazar). MCP
yanıtı kısa özet döner (kaç kitap indirimli). Müşteri "roman kitaplarını getir" deyince dönen kitaplar
indirimli fiyatı + bitiş tarihini satır içinde taşır.

**Why this priority**: Feature'ın görünür değeri; indirim açılamaz + müşteriye görünmezse ürün yok.

**Independent Test**: Kategori süzgeciyle kampanya aç → `query_storefront` ile o kategoriyi listele →
kitaplar indirimli fiyat + `discount_ends_at` taşır; kampanyasız kitap liste fiyatını taşır.

**Acceptance Scenarios**:

1. **Given** Roman'da 3 yayınlı kitap (indirimsiz), **When** admin kategoriye %20 açar, **Then** üçü de
   `discount_pct=20`, `effective_price = liste × 0.8`, `discount_ends_at` dolu döner.
2. **Given** Roman'daki bir kitapta zaten aktif indirim var, **When** admin kategoriye %20 açar, **Then**
   o kitabın indirimi %20 ile EZİLİR (son-gelen-kazanır), diğerleri de %20 alır; hepsi kapsamda.
3. **Given** tek-kitap süzgeci, **When** admin "şu kitaba %15" der, **Then** yalnız o kitap %15 indirimli.
4. **Given** aktif indirim, **When** müşteri o kitapla checkout yapar, **Then** ödenecek tutar Discount.Api
   gRPC'den gelen yüzdeye göredir (vitrin snapshot değil, canlı doğrulama).

---

### User Story 2 - Süre dolunca kitaplar otomatik listeye döner (Priority: P1)

Kampanyanın bitiş tarihi geçince o kampanyanın kitapları indirimli fiyattan liste fiyatına kendiliğinden
döner; kimse elle tetiklemez.

**Why this priority**: Süresi geçmiş indirim satmak yanlış fiyatlandırma; güvenilirlik şartı.

**Independent Test**: Bitişi yakın kampanya aç → süre dolunca vitrin o kitaplar için liste fiyatını gösterir.

**Acceptance Scenarios**:

1. **Given** bitişi T'ye kurulu kampanya, **When** T geçer, **Then** kampanyanın kitaplarının `ProductDiscount`
   kaydı temizlenir, vitrin liste fiyatını gösterir, `discount_pct` boş döner.
2. **Given** Discount.Api bitiş anında kapalıydı, **When** yeniden açılır, **Then** kaçan bitiş durable
   telafi edilir (geç ama uygulanır). Bu boşlukta view-guard `now>ends_at` indirimi zaten gizler.

---

### User Story 3 - Sepette expiry: grace yok (Priority: P2)

Ürün sepetteyken kampanya biterse checkout indirim uygulamaz; müşteri liste fiyatını öder.

**Why this priority**: Fiyat doğruluğu; sepete-ekleme anını kilitlemek ek state + tutarsızlık.

**Acceptance Scenarios**:

1. **Given** indirimli kitap sepette, **When** kampanya biter sonra `start_payment`, **Then** tutar LİSTE
   fiyatı (gRPC aktif yüzde döndürmez), indirim bağlanmaz.

### Edge Cases

- Kampanya başlangıcı gelecekteyse: tanımlanır ama başlangıca dek uygulanmaz (vitrin liste fiyatı).
- Yüzde ≤0/≥100, bitiş < başlangıç, eksik/geçersiz süzgeç referansı (scopeRef boş ya da scopeType ile
  tutarsız): aggregate/handler reddeder.
- Geçerli süzgeç hiç kitaba çözülmezse (scopeRef sağlam ama 0 ürün eşleşir): kampanya AÇILIR ama 0 kitap;
  özet "0 kitap" der (red DEĞİL — L93'teki eksik-scopeRef reddinden ayrı).
- Kampanyalı kitabın liste fiyatı değişirse: vitrin etkin fiyatı yeni listeden kendiliğinden hesaplar.
- Kampanya iptal edilirse: o kampanyanın kitaplarının indirimi temizlenir (push).
- Aynı kitap iki kampanya süzgecine de girse (ör. hem "Roman" hem "şu yazar"): SON uygulanan kazanır,
  kitabın kaydı en son kampanyanın yüzdesiyle ezilir (kitap başına tek etkin indirim).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem admin'e `/mcp-admin`'den indirim kampanyası açmayı sağlamalı: süzgeç (kategori ·
  yazar · yayınevi · tek-kitap) + yüzde + başlangıç + opsiyonel bitiş.
- **FR-002**: Sistem süzgeci **aktifleşme anında** (startsAt≤now ise create, aksi halde start-fire)
  somut kitap setine çözmeli (snapshot) ve her kitaba `ProductDiscount` **yazmalı — varsa ÜZERİNE yazar**
  (son-gelen-kazanır; atlama YOK). Gelecek tarihli (Scheduled) kampanya aktifleşene dek `ProductDiscount`
  YAZMAZ — slot rezerve etmez. Çözüm/store/push tek atomik aktifleşme adımıdır.
- **FR-003**: Sistem kitap başına indirim yüzdesini + pencereyi vitrin read-model'ine **itmeli**; vitrin
  etkin fiyatı kendi liste fiyatından hesaplayıp `query_storefront`'ta inline döndürmeli.
- **FR-004**: Sistem kampanya başlangıç + bitişini **per-kampanya dayanıklı scheduled message** ile
  yönetmeli; bitişte o kampanyanın kitaplarının indirimini temizleyip itmeli. Handler guard'lı idempotent
  (bayat mesaj no-op); restart'ta kaçan fire durable telafi edilmeli. Vitrin ayrıca `now` pencere dışıysa
  indirimi gizlemeli (view-guard yedek).
- **FR-005**: Sistem checkout'ta kitapların aktif indirim yüzdesini **canlı doğrulamalı** (gRPC); ödenecek
  tutar vitrin snapshot'ına değil bu cevaba dayanmalı.
- **FR-006**: Sistem admin'e kampanya LİSTELEME + İPTAL sağlamalı; iptal o kampanyanın kitaplarının
  indirimini temizlemeli.
- **FR-007**: Sistem geçersiz kampanyayı reddetmeli: yüzde ≤0 veya ≥100, bitiş < başlangıç, boş ad, **eksik
  scopeRef** ya da **scopeType↔scopeRef tutarsızlığı** (ör. ScopeType=Product ama scopeRef bir kategori id).
  NOT: geçerli ama 0 kitaba çözülen süzgeç red DEĞİL — kampanya 0 kitapla açılır (bkz. Edge Cases).

### Kapsam dışı

- Kupon (kod, apply, redemption, per-müşteri limit, keşif) → G6.1.
- Sabit-tutar indirim (yalnız yüzde v1).
- En-iyi-kazanır / dinamik fiyat kuralı (gelecekteki kitapları otomatik kapsayan canlı boyut) → ileri seviye.
- Değişen-kitaplar rapor maili (admin'e düz-liste mail) → D16 tech-debt, 079 dışı.
- AdminActionLog, Excel.

### Key Entities *(include if feature involves data)*

- **Campaign**: kampanya aggregate. Süzgeç tipi (Category/Author/Publisher/Product) + süzgeç referansı,
  yüzde, başlangıç, opsiyonel bitiş, durum. Pencereyi + hangi süzgeçle açıldığını taşır (denetim/iptal).
  Invariant: yüzde 1-99, bitiş > başlangıç.
- **ProductDiscount**: kitap başına indirim kaydı. `{productId (PK), campaignId, percentage, startsAt,
  endsAt}`. PK teklik = kitapta tek etkin indirim; yeni kampanya kaydı **ezer** (son-gelen-kazanır). Discount.Api **fiyat tutmaz**.
- **ProductCatalogRef** (destek read-model): Catalog event'inden `{productId, categoryId, authorIds,
  publisherId, published}` — süzgeci kitap setine çözmek için (fiyat/isim tutmaz).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin bir kategori kampanyasını tek komutla açar; kapsamdaki tüm kitaplar 5 sn
  içinde vitrinde indirimli görünür.
- **SC-002**: Kampanya bitişinden sonra o kampanyanın hiçbir kitabı indirimli fiyat göstermez (elle müdahale yok).
- **SC-003**: Hiçbir kitapta aynı anda birden çok ETKİN indirim olmaz; yeni kampanya kitabın indirimini ezer
  (son-gelen-kazanır → her kitap için tek yüzde vitrinde/checkout'ta).
- **SC-004**: Checkout'ta ödenen tutar kitabın aktif indirim yüzdesiyle %100 eşleşir (vitrin-checkout sapması yok).

## Assumptions

- Discount.Api yeni BC = kendi `discountDb`'si + Marten şeması; entegrasyon yalnız event + checkout gRPC.
- Storefront read-model'i indirim alanları (`discount_pct`, `starts_at`, `ends_at`) kazanır; etkin fiyatı
  kendi liste fiyatından hesaplar; `query_storefront` bunları döner (067/069 view'i genişler).
- Süzgeç çözümü için gereken ürün↔yazar/yayınevi/kategori bilgisi `ProductChangedEvent`'te mevcut (Authors[],
  PublisherId, CategoryId) → Discount.Api kendi `ProductCatalogRef` kopyasına besler.
- Admin yüzeyi MCP-only (`/mcp-admin`, açık allowlist — 070/074 deseni); domain iş REST'i açılmaz.
- Kampanya verisi tümüyle admin komutlarından doğar; Excel/import indirim taşımaz.