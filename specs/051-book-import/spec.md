# Feature Specification: First-Party Kitap Toplu Import

**Feature Branch**: `051-book-import`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "First-party kitap toplu import (bookstore roadmap adım 1). Amazon popular books dataset'ten yalnız ISBN10'lu kitapları Catalog'a topluca bas; fiyatsız ürün yayına çıkmasın; kapak eksikse yine yayınlansın (placeholder); omurga (Catalog→Stock→Storefront) event'le uyansın. İş ayrımı: İş1 build-time veri şekillendirme (Catalog dışı), İş2 aggregate yaratma+yayın (Catalog). Yeni servis yok."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kitaplar mağazaya toplu girer (Priority: P1)

Mağaza sahibi first-party kitapçıyı açıyor. Elinde dataset'ten süzülmüş, ISBN'li kitap listesi (build-time şekillendirme çıktısı) var. Sistem açıldığında bu kitaplar toplu olarak katalog'a basılır — elle tek tek giriş yok. Kimlik ISBN'den türer; ad, yazar, fiyat (TL), kapak linki eşlenir.

**Why this priority**: Kitap verisi olmadan mağazanın hiçbir sonraki adımı (öneri, fiyat, indirim, satış) çalışamaz. Tüm roadmap'in kök adımı.

**Independent Test**: Boş sistem açılır; import sonrası katalog'da ISBN'li kitapların yayına uygun olanları görünür; sayısı ve alanları (ad/yazar/fiyat/kapak) beklenen liste ile tutar.

**Acceptance Scenarios**:

1. **Given** boş katalog + süzülmüş kitap listesi, **When** import çalışır, **Then** ISBN'li her benzersiz kitap katalog'a tek kez yazılır.
2. **Given** import bir kez çalışmış, **When** import tekrar çalışır, **Then** kitaplar çoğaltılmaz — aynı ISBN güncellenir (idempotent).
3. **Given** aynı ISBN'in listede iki kez geçmesi, **When** import çalışır, **Then** tek kitap yazılır (tekilleştirme).

---

### User Story 2 - Fiyatsız kitap yayına çıkmaz, kapaksız çıkar (Priority: P1)

Mağaza sahibi fiyatsız (kırık) ürün istemiyor: fiyatı olmayan kitap vitrine düşerse "Sepete ekle" var ama fiyat yok — satılamaz. Fiyatı olmayan kitap içeri girer ama **yayında olmaz** (taslak kalır). Buna karşılık kapak görseli eksikse bu tek başına engel değildir; kitap yayınlanır, vitrinde **placeholder görsel** ile gösterilir.

**Why this priority**: Yayın kalitesi kapısı; fiyatsız ürünle vitrin kirlenmesini engeller ama kapak gibi kozmetik eksik için satılabilir kitabı gizlemez. Sonraki adımda (ML fiyat) taslaklar yayına geçebilir.

**Independent Test**: Fiyatı boş kitap import edilir; katalog'da taslak durur, vitrinde/stokta yok. Kapağı boş ama fiyatlı kitap import edilir; vitrinde placeholder ile görünür ve satın-alınabilir.

**Acceptance Scenarios**:

1. **Given** fiyatı olan kitap, **When** import çalışır, **Then** yayınlanır ve vitrinde görünür.
2. **Given** fiyatı olmayan (veya sıfır) kitap, **When** import çalışır, **Then** taslak kalır; vitrinde/stokta görünmez.
3. **Given** fiyatı olan ama kapağı olmayan kitap, **When** import çalışır, **Then** yayınlanır; vitrinde placeholder görsel gösterilir.
4. **Given** açıklaması boş ama fiyatlı kitap, **When** import çalışır, **Then** yayınlanır (açıklama zorunlu değil).

---

### User Story 3 - Yayınlanan kitap stok ve vitrine yansır (Priority: P1)

Kitap yayınlandığında, mağaza envanterinde başlangıç stoğuyla belirir (mallar mağazanın) ve müşteri-görünür vitrinde listelenir. Böylece kitap gerçekten satılabilir olur. Bu yansımalar **event'le** olur; Stock ve Storefront verisi Catalog ürününden türer.

**Why this priority**: "Satılabilir" olmak = katalogta olmak yetmez; stok kaydı + vitrin satırı gerekir. Omurga bu adımda uyanır.

**Independent Test**: Kitap yayınlanır; envanterde başlangıç adediyle (satın-alınabilir), vitrinde listelenir. Taslak kitap ne stokta ne vitrinde.

**Acceptance Scenarios**:

1. **Given** yayınlanan kitap, **When** yayın gerçekleşir, **Then** envanterde başlangıç stoğuyla görünür.
2. **Given** yayınlanan kitap, **When** yayın gerçekleşir, **Then** müşteri-görünür vitrinde listelenir.
3. **Given** taslak (fiyatsız) kitap, **When** import biter, **Then** ne envanterde ne vitrinde yer alır (hiç event yayılmaz).

---

### Edge Cases

- **Aynı ISBN iki kez** (listede çakışma): tekilleştir — tek kitap.
- **Fiyatı sıfır/boş**: yayınlanmaz (taslak). Sıfır fiyat = "fiyatsız".
- **Kapak linki boş**: yayınlanır; vitrin placeholder görsel gösterir.
- **Açıklama boş**: yayına engel değil.
- **Import yarıda kesilir**: tekrar çalışınca kaldığı yeri bozmadan tamamlar (idempotent; sıfır çoğaltma).
- **ISBN'siz dataset kaydı** (dijital/ASIN-only): İş1'de zaten süzülür, listeye girmez.
- **Kur**: liste fiyatı TL (İş1'de USD→sabit kur çevrildi); Catalog TL alır, kur mantığı taşımaz.

## Requirements *(mandatory)*

### Functional Requirements

**İş 1 — Veri şekillendirme (Catalog dışı, build-time):**

- **FR-001**: Ham dataset (~20MB) repoya **girmemeli**; yalnız süzülmüş, gereken alanları taşıyan küçük veri dosyası commit edilmeli.
- **FR-002**: Şekillendirme **yalnız ISBN'li** kayıtları tutmalı; ISBN'siz (dijital/ASIN-only) kayıtları atmalı.
- **FR-003**: Şekillendirme her kitabı ISBN ile **tekilleştirmeli**; fiyatı USD'den **sabit kur ile TL'ye** çevirmeli (canlı kur yok).
- **FR-004**: Çıktı her kitap için şu alanları taşımalı: ISBN, ad (başlık), yazar (brand), fiyat (TL, yoksa boş), kapak dış link (yoksa boş). Açıklama, ağırlık, ölçü, format, puan/yorum-sayısı, ASIN **alınmamalı**.

**İş 2 — Aggregate yaratma + yayın (Catalog.Api):**

- **FR-005**: Catalog, süzülmüş listeyi okuyup her kitabı ürün olarak **yazmalı**; ağır parse/ayıklama İş1'de olduğundan Catalog ince kalmalı.
- **FR-006**: Ürün kimliği (ProductId) **ISBN'den deterministik** türetilmeli; aynı ISBN her zaman aynı ProductId'yi vermeli (idempotency + servisler-arası ortak anahtar).
- **FR-007**: Import **idempotent** olmalı; tekrar çalışınca aynı ISBN'i çoğaltmadan güncellemeli.
- **FR-008**: **Fiyatı olmayan** kitap içeri alınmalı ama **yayınlanmamalı** (taslak durumda tutulmalı).
- **FR-009**: Yayın için tek zorunlu alan: **fiyat > 0**. Kapak linki ve açıklama yayın için zorunlu **değil**.
- **FR-010**: Yayın kararı **ürün aggregate'inin davranışı** olmalı (kural handler'da değil, aggregate metodunda); taslak→yayın geçişi kalıcı olmalı.
- **FR-011**: Kitap **yayınlandığında**, envanter otoritesine kimlik (barkod↔ProductId) + başlangıç stoğu (sabit varsayılan adet) **event ile** bildirilmeli; kitap satın-alınabilir olmalı.
- **FR-012**: Kitap **yayınlandığında**, müşteri-görünür vitrine **event ile** yansımalı (vitrin verisi Catalog ürününden türer; vitrin sahibi olmayan hiçbir bileşen vitrine doğrudan yazmamalı).
- **FR-013**: Kapak linki olmayan yayınlanmış kitap, vitrinde **placeholder görsel** ile gösterilmeli (kayıp görsel satırı gizlemez/kırmaz).
- **FR-014**: **Taslak** (fiyatsız) kitap hiçbir event yaymamalı → ne envantere ne vitrine yansımamalı.
- **FR-015**: Ürün-yayın olayının adı first-party modele uygun olmalı; önceki feed-kaynaklı ad (`ProductLinked`) yeni ada (`ProductAdded`) taşınmalı.

**Kapsam sınırı:**

- **FR-016**: İndirim, format-varyantları, açıklama/fiyat tamamlama (ML), yeni ingestion servisi bu feature'da **yok**; sonraki adımlara bırakılmalı.

### Key Entities

- **Kitap (Product / Catalog)**: Zengin ürün aggregate'i. Kimlik ISBN-türevli ProductId + ISBN(Gtin), ad, yazar, fiyat (TL), kapak linki + **yayın durumu (Taslak/Yayında)**. Yayın davranışı zorunlu-alan invariant'ını (fiyat>0) taşır.
- **Başlangıç Stok kaydı (Stock)**: Yayınlanan kitabın envanterdeki ilk OnHand'i + barkod↔ProductId eşlemesi. Catalog event'inden türer.
- **Vitrin satırı (Storefront view)**: Müşteri-görünür denormalize kitap satırı; yalnız yayınlanan kitaplardan, Catalog event'iyle push-only doğar. Kapak yoksa placeholder gösterir.
- **Süzülmüş kitap veri dosyası**: İş1 çıktısı; yalnız ISBN'li kitapları + gereken alanları taşıyan commit'li artefakt.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Boş sistemden tek import ile ISBN'li tüm benzersiz kitaplar (≈1427) katalog'a girer; manuel tek-tek giriş sıfır.
- **SC-002**: Fiyatı olmayan hiçbir kitap müşteri vitrininde görünmez (%100 kapı).
- **SC-003**: Fiyatı olan her kitap import sonrası vitrinde listelenir ve başlangıç stoğuyla satın-alınabilir; kapağı olmayanlar placeholder ile görünür.
- **SC-004**: Import ikinci kez çalıştığında katalog kitap sayısı değişmez (idempotent; sıfır çoğaltma).
- **SC-005**: Repoya eklenen veri dosyası ham dataset'ten belirgin küçüktür (yalnız gereken alanlar + ISBN alt-kümesi).
- **SC-006**: Aynı ISBN her çalıştırmada aynı ProductId'yi verir (deterministik kimlik doğrulanır).

## Assumptions

- **Başlangıç stok** = sabit varsayılan adet (öneri **100**); dataset güvenilir adet taşımaz, first-party = mağaza belirler. `availability` serbest-metni parse edilmez.
- **Yayın kapısı** = yalnız fiyat>0. Bu setle yalnız ≈34 fiyatsız kitap taslak kalır; ≈12 kapaksız dahil gerisi yayınlanır (kapaksızlar placeholder ile).
- **Placeholder görsel**: kapak linki boş olan yayınlanmış kitaplar için sabit yer-tutucu (WebApp mevcut görsel gösterim yolundan).
- **Açıklama** boş bırakılır (dataset %54 boş + HTML kirli); tamamlama sonraki adım (ML / grounded generation).
- **Silme yok**: ürün silme mevcut kararla kapsam dışı; import yalnız ekler/günceller.
- **Varyant (format baskıları)** kapsam dışı; her kitap tek ürün (düz ≈1427). Varyant sonraki feature.
- **İndirim** kapsam dışı (ayrı Discount/Pricing BC, roadmap adım 4); base fiyata dokunulmaz.
- **Kaynak veri**: Amazon popular books dataset (2269 kayıt; 1429 ISBN'li, 2 dupe→1427 tekil). İş1'de süzülür.
- **Tetik**: sistem açılışında bir kez çalışan seeder (elle giriş yok, toplu liste); idempotent olduğu için her boot güvenli.
- **Yeni servis yok**: ingestion domain'i (süreklilik/dış-kaynak/kendi-invariant'ı/operasyonel-izolasyon) bu one-shot first-party seed'de oluşmadığından ayrı servis açılmaz.