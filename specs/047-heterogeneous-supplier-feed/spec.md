# Feature Specification: Heterogeneous Supplier Feed (ACL) + Buy-box Teardown

**Feature Branch**: `047-heterogeneous-supplier-feed`

**Created**: 2026-08-23

**Status**: Draft

**Input**: User description: "Supplier.Api heterojen feed + Procurement adapter-per-supplier (ACL). Her
tedarikçi kendi route'u + kendi farklı JSON şeklini döner; Procurement her tedarikçi için ince ACL
adapter'ıyla iç modele normalize eder. Buy-box rekabeti BIRAKILDI: barkod global tekil → çoklu-offer
buy-box makinesi sökülür."

## Bağlam ve Amaç *(neden)*

İki bağımsız karar bu feature'da birleşiyor:

**(1) Heterojen feed gerçekçiliği.** Bugün iki tedarikçi **tek dış uçtan** (aynı host, path-param) ve
**tek ortak JSON şeklinden** çekiliyor; bu "iki bağımsız dış tedarikçi" illüzyonunu zayıf tutar. Gerçek
dropship'te her tedarikçi kendi sistemidir — kendi feed adresini ve kendi veri şeklini konuşur.

**(2) Buy-box bırakıldı → barkod global tekil.** Barkodları datasetlerde elle üretiyoruz; kaza
çarpışması (farklı ürünler aynı barkod) çok-satan ürünü alakasız yeniyle tek kayda merge edip kadük
edebilir. Bu risk buy-box rekabetinden doğuyordu. Rekabet tümden bırakıldı: her barkod tek tedarikçiye
ait; çoklu-offer/buy-box makinesi artık ölü → **sökülür** (uykuda bırakılmaz).

Değer: entegrasyon sınırı gerçek hayattaki gibi belirginleşir (yabancı şekli soğuran çeviri katmanı) ve
tüketilmeyen buy-box karmaşıklığı boru hattından + kontrattan temizlenir.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Farklı-şekilli tedarikçiden ürün çekme (Priority: P1)

Platform, veri şekli birbirinden farklı iki tedarikçiden ürünleri çeker ve her ikisini de aynı iç
kanonik ürün havuzuna tutarlı yerleştirir. Bir tedarikçi ürünü `barcode`/`price`/`stock` kelimeleriyle,
öteki `gtin`/`cost`/`warehouseQty` kelimeleriyle tanımlasa bile, çekim sonrası havuzda her ikisi de aynı
anlamsal alanlara (barkod, fiyat, stok, ad, marka…) oturur.

**Why this priority**: Feature'ın çekirdek değeri — heterojen kaynağı tek iç modele indirgemek. Bu
olmadan geri kalanı anlamsız; tek başına teslim edilince "farklı formatlı tedarikçiyi kattık" değerini
verir.

**Independent Test**: İki farklı-şekilli dataset hazırla, çekimi tetikle, havuzdaki iki ürünün de
barkod/fiyat/stok/ad alanlarının doğru dolduğunu doğrula (kelime farkına rağmen).

**Acceptance Scenarios**:

1. **Given** tedarikçi A `barcode/name/price/stock` şeklinde bir ürün yayınlıyor, **When** çekim
   çalışır, **Then** havuzda o barkodda, fiyat ve stoğu doğru dolmuş bir kanonik ürün oluşur.
2. **Given** tedarikçi B *aynı anlamı farklı kelimelerle* (`gtin/title/cost/warehouseQty`) yayınlıyor,
   **When** çekim çalışır, **Then** havuzda `gtin` barkoda, `cost` fiyata, `warehouseQty` stoğa
   çevrilmiş bir kanonik ürün oluşur.
3. **Given** bir tedarikçinin şeklinde beklenen alanlardan biri eksik/boş, **When** çekim çalışır,
   **Then** o satır için belirlenmiş davranış uygulanır (bkz. Edge Cases), diğer satırları ve diğer
   tedarikçiyi etkilemez.

---

### User Story 2 - Buy-box/çoklu-offer sökümü (barkod-başı tek tedarikçi) (Priority: P1)

Barkod global tekil kabul edildiğinden her kanonik ürün **tek tedarikçiye** aittir. Çoklu-offer
seçimi (buy-box), buy-box-değişti bildirimi ve onu tüketen aşağı-akım handler'ları **kaldırılır**.
Fiyat/stok değişimleri artık **tek kanonik-güncelleme kanalından** akar; ayrı bir buy-box olayı yoktur.

**Why this priority**: Kararın somut bedeli/temizliği. Barkod tekilse buy-box ölü kod + ölü kontrat
demektir; bırakılırsa yanıltıcı karmaşıklık kalır. Söküm bu feature'ın açık talebi.

**Independent Test**: Bir barkodu tek tedarikçiden çek; fiyat/stok değişince Catalog/Stock'un tek
kanonik-güncelleme olayıyla güncellendiğini, hiçbir buy-box olayı üretilmediğini/tüketilmediğini
doğrula. Kod tabanında buy-box seçim/olay/handler kalıntısı olmadığını doğrula.

**Acceptance Scenarios**:

1. **Given** bir barkod tek tedarikçiden çekiliyor, **When** o tedarikçinin fiyatı/stoğu değişir,
   **Then** Catalog fiyatı ve Stock miktarı **tek kanonik-güncelleme olayıyla** güncellenir; ayrı
   buy-box olayı yayınlanmaz.
2. **Given** bir ürün feed'den düşer (delist), **When** çekim çalışır, **Then** kanonik ürün ilgili
   davranışı uygular (satılamaz/işaretli) — rakip-kazanır mantığı **yoktur** (tek tedarikçi).
3. **Given** kod tabanı derlenir/test edilir, **When** buy-box seçim mantığı, buy-box-değişti olayı ve
   onun Catalog/Stock handler'ları aranır, **Then** hiçbiri bulunmaz (tümü sökülmüş).

---

### User Story 3 - Her tedarikçi kendi ucundan okunur (Priority: P2)

Platform her tedarikçiyi ayrı bir feed adresinden okur; hangi tedarikçinin hangi uçtan okunacağı
yapılandırmayla belirlenir. Yeni tedarikçi ucu = tedarikçi kaydına adres eklemek.

**Why this priority**: Topolojik gerçekçilik + genişleyebilirlik. Entegrasyon sınırını gerçek dünyaya
oturtur, ama P1 çevirisi olmadan tek başına sınırlı değer taşır.

**Independent Test**: İki tedarikçinin farklı uçlardan, doğru datasetle okunduğunu doğrula; eksik/yanlış
adreste o tedarikçinin atlanıp diğerinin çekilmeye devam ettiğini gör.

**Acceptance Scenarios**:

1. **Given** iki tedarikçi ayrı uçlarla yapılandırılmış, **When** zamanlanmış çekim çalışır, **Then**
   her tedarikçi kendi ucundan kendi datasetiyle okunur.
2. **Given** bir tedarikçinin ucu tanımsız, **When** çekim çalışır, **Then** o tedarikçi atlanır (hata
   kaydedilir), diğerlerinin çekimi kesintisiz sürer.

---

### User Story 4 - Feed değişimi dataset dosyasını düzenleyerek simüle edilir (Priority: P3)

Her tedarikçinin verisi kendi dataset dosyasında yaşar ve dosya **istek anında** okunur; feed değişimini
simüle etmek için o tedarikçinin dosyası elle düzenlenir, sonraki çekim yeni veriyi görür. Ayrı bir
"sürüm ilerlet" ucu **yoktur** (fazlalık — sökülür); tedarikçiler yalıtık (birinin dosyası ötekini
etkilemez).

**Why this priority**: Test/deney kolaylığını sadeleştirir; yeni yetenek değil. `advance`/rev makinesi
kaldırılınca tek dosya + canlı düzenleme yeterli.

**Independent Test**: A'nın dataset dosyasını düzenle, sonraki çekimde A'nın verisinin değiştiğini,
B'nin değişmediğini doğrula.

**Acceptance Scenarios**:

1. **Given** A dataset dosyası düzenlendi, **When** sonraki çekim çalışır, **Then** A yeni veriyi verir,
   B etkilenmez.
2. **Given** kod tabanı derlenir, **When** feed sürüm-ilerletme ucu (`advance`) aranır, **Then**
   bulunmaz (sökülmüş).

---

### Edge Cases

- **Çeviride zorunlu alan eksik/boş** (barkod yok): o satır **atlanır** + hata kaydedilir; çekim geri
  kalanla sürer (per-satır fail-open, eksik-kimlikte fail-closed).
- **Tedarikçi şekli beklenenden sapıyor** (tanınmayan/bozuk gövde): o tedarikçinin çekimi hata kaydıyla
  **atlanır**, diğerleri etkilenmez.
- **Aynı barkod iki farklı tedarikçide**: bu feature **üretmez** — barkod global tekil. Söküm sonrası
  havuz zaten barkod-başı tek tedarikçi bekler; örtüşme gelirse bu bir veri-hatası kabul edilir (guard
  ayrı açık araştırma, bkz. Assumptions).
- **`gtin` vs `barcode` kelime farkı**: ikisi de tek kanonik barkoda çevrilir; iç model tek kelime bilir.
- **Sayısal alan çevrilemiyor** (metin/para birimi/ondalık): çeviri ayrıştıramazsa satır atlanır + hata
  kaydedilir (sessiz yanlış-değer yasak).
- **Yalnız fiyat/stok değişti**: tek kanonik-güncelleme olayı bunu taşır (ayrı buy-box olayı yok);
  içerik aynıyken bile fiyat/stok değişimi aşağı-akıma iletilir.

## Requirements *(mandatory)*

### Functional Requirements

**Heterojen feed + ACL**

- **FR-001**: Sistem her tedarikçiyi **kendi ayrı feed ucundan** okuMALI; eşleşme **yapılandırmadan**
  gelMELİ (kod-içi sabit adres değil).
- **FR-002**: Her tedarikçi feed'i **kendi veri şeklini** döndürebilMELİ; sistem tek ortak şekil
  varsaymaMALI.
- **FR-003**: Sistem her tedarikçinin yabancı şeklini tedarikçiye-özel bir **çeviri katmanıyla** (ACL)
  iç kanonik satıra normalize etMELİ (barkod, ad, marka, kategori, fiyat, stok, boyutlar, öznitelikler,
  aile-kodu).
- **FR-004**: Çeviri farklı kelimeleri (ör. `gtin`↔barkod, `cost`↔fiyat, `warehouseQty`↔stok) **tek
  kanonik anlama** eşleMELİ; iç model tedarikçi kelime farkını görmeMELİ.
- **FR-005**: Bir tedarikçinin çeviri/okuma hatası **yalıtılMALI**; o tedarikçi/satır atlanıp
  kaydedilirken diğerlerinin çekimi sürMELİ.
- **FR-006**: Zorunlu kimlik alanı (barkod) çeviremeyen satır **atlanMALI** + hata kaydedilMELİ;
  sessizce yanlış/boş kimlikli ürün yaratılmaMALI.
- **FR-011**: Yeni tedarikçi eklemek çekirdek havuz/çekim boru hattını değiştirmeden — yalnız ucu
  yapılandırıp çeviri katmanı ekleyerek — mümkün olMALI.
- **FR-012**: Örnek datasetler **elle** düzenlenMELİ (kod-üretici mock reddedildi); yeni feed alanı hem
  o tedarikçinin feed şekline hem çeviri katmanına eklenMELİ, yoksa çeviride düşMELİ.

**Buy-box sökümü (barkod-başı tek tedarikçi)**

- **FR-020**: Sistem her barkodu **tek tedarikçiye** ait kabul etMELİ; barkod-başı çoklu-offer tutma
  kaldırılMALI.
- **FR-021**: Çoklu-offer **seçim mantığı** (en-düşük-fiyat/priority buy-box değerlendirmesi)
  kaldırılMALI.
- **FR-022**: Ayrı **buy-box-değişti olayı** ve onu tüketen Catalog/Stock handler'ları kaldırılMALI.
- **FR-023**: Fiyat/stok değişimleri **tek kanonik-güncelleme olayından** akMALI; içerik aynıyken bile
  fiyat/stok değişimi bu olayla aşağı-akıma iletilMELİ.
- **FR-024**: Delist davranışı **tek-tedarikçi** varsayımına indirgenMELİ; "rakip kazanır" mantığı
  kaldırılMALI.
- **FR-025**: Tedarikçi **önceliği**nin tek anlamı merge-sırasıydı; çoklu-offer gidince priority artık
  kanonik içeriğe **etki etmeMELİ** (alan seed'te kalabilir ama seçim/merge'de kullanılmaMALI).
- **FR-026**: `CanonicalProductUpserted` ve `ProductLinked` olayları **korunMALI**; barkod→ürün kimliği
  flip'lerde/güncellemelerde **sabit** kalMALI.
- **FR-027**: Söküm sonrası kod tabanında buy-box seçim/olay/handler **kalıntısı olmaMALI** (ölü kod
  bırakılmaz).

**Tek-gate sadeleştirme + mock uç temizliği**

- **FR-030**: Listing-düzeyi değişim-tespiti (`ListingChange` enum, listing içerik-hash erken-çıkışı)
  **kaldırılMALI**; idempotency **tek noktada** — yayın kararında (yayınlanmış içerik/fiyat/stok
  karşılaştırması) — toplanMALI.
- **FR-031**: Tek-gate sonrası downstream event davranışı korunMALI: içerik/fiyat/stok gerçekten
  değişmediyse hiçbir integration event yayınlanmaMALI (tekrar-pull sessiz kalır).
- **FR-032**: Mock feed "sürüm ilerlet" ucu (`advance`) ve rev makinesi **kaldırılMALI**; tedarikçi başına
  **tek dataset dosyası** kalMALI (istek anında okunur, canlı düzenleme yansır).

### Key Entities *(include if feature involves data)*

- **Tedarikçi Feed Şekli (tedarikçiye özel)**: Bir tedarikçinin dış dünyada konuştuğu ham veri
  sözleşmesi; alan adları/şekli tedarikçiden tedarikçiye farklı (yabancı kontrat).
- **Çeviri Katmanı / Adapter (ACL)**: Bir tedarikçinin ham şeklini iç kanonik satıra çeviren ince,
  tedarikçiye bağlı birim; girdi = ham şekil, çıktı = iç kanonik satır.
- **İç Kanonik Satır**: Ingestion'ın havuza yazdığı, tedarikçi-bağımsız normalize model (barkod, ad,
  marka, kategori, fiyat, stok, boyut, öznitelik, aile-kodu).
- **Havuz Ürünü (barkod-başı tek tedarikçi)**: Bir barkoda karşılık **tek** tedarikçi kaydı; artık
  çoklu-offer koleksiyonu **yok**.
- **Tedarikçi Kaydı**: Kimlik + **feed ucu adresi** (yapılandırmadan); priority alanı kalsa da
  seçim/merge'de kullanılmaz.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Veri şekli farklı en az iki tedarikçiden çekilen ürünler, çekim sonrası havuzda %100 aynı
  iç kanonik alanlara (barkod/ad/fiyat/stok) doğru oturur.
- **SC-002**: Bir tedarikçinin okuma/çeviri hatası diğer tedarikçilerin çekilen ürün sayısını
  düşürmez (bir kaynağın çökmesi diğerini 0 etkiler).
- **SC-003**: Yeni, farklı-şekilli üçüncü tedarikçi eklemek çekirdek havuz/çekim kodunu değiştirmeden —
  yalnız yapılandırma + tek çeviri birimi ekleyerek — tamamlanabilir.
- **SC-004**: Zorunlu kimliği (barkod) çevrilemeyen hiçbir satır havuza yazılmaz; böyle satırların
  %100'ü görünür hata kaydedilir.
- **SC-005**: Bir tedarikçinin feed sürümünü ilerletmek diğerinin verisini değiştirmez.
- **SC-006**: Söküm sonrası bir barkodun fiyat/stok güncellemesi Catalog/Stock'a **tek** olay tipiyle
  ulaşır; buy-box seçim/olay/handler koda dair arama **sıfır** sonuç verir.
- **SC-007**: Barkod→ürün kimliği güncellemeler boyunca sabit kalır (yorum/liste/URL kopmaz).
- **SC-008**: Değişmemiş feed'in tekrar çekilmesi sıfır integration event üretir (tek publish-gate
  korunur); kod tabanında `ListingChange` ve `advance` araması sıfır sonuç verir.

## Assumptions

- Supplier.Api **tek process** kalır; "ayrı uç" = aynı host içinde tedarikçi-başı ayrı route (ayrı
  Aspire servisi kapsam DIŞI).
- Başlangıç kapsamı **iki tedarikçi = iki ince çeviri birimi**; genel plugin/keşif çerçevesi kurulmaz
  (düz kod tercihi).
- Çekim/havuz dayanıklılığı (idempotent upsert, hash-diff, retry, error queue) korunur; söküm yalnız
  buy-box seçim/olay/handler katmanını kaldırır.
- **Barkod tekillik-guard** implementasyonu bu feature'da **yok** — ayrı açık araştırma (Obsidian
  `supplier-realism-barcode-competition-open-question`); tekillik burada yalnız **elle** garanti edilir.
- Delist artık tek-tedarikçi; barkod tekil olduğundan "rakip kazanır" yolu zaten erişilemez.
- Tedarikçi feed ucu adresleri servis-keşfi + yapılandırma üzerinden çözülür; ağ/kimlik-doğrulama
  gerçekçiliği (API key vb.) kapsam DIŞI.