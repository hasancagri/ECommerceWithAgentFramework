# Feature Specification: Kitap Yazar + Yayınevi Modeli

**Feature Branch**: `052-book-author-publisher`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "Kitap yazar + yayınevi modeli (katalog+detay backend omurgası; kitapyurdu kuzey-yıldızı). Brand→Author çok-yazar, yeni Publisher aggregate, contributors display-only, çevirmen yok, veri temizliği, Storefront facet Author+Publisher."

## Kuzey-yıldızı

Storefront UX'i **kitapyurdu.com**'a hizalanır. Bir kitabın künyesi orada yazar / yayınevi / çevirmen olarak ayrı ayrı, her biri gezilebilir link biçiminde durur. Bu feature o künyenin **veri omurgasını** kurar: yazar ve yayınevi gezilebilir eksen olur; sayfa (frontend) ayrı bir adımdır.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Yazar ve yayınevine göre süzme (Priority: P1)

Ayşe kitap katalogunu gezerken sol taraftaki filtrelerden bir yazarı ya da bir yayınevini seçip listeyi daraltır. "Emily Brontë" seçince o yazarın tüm kitapları; "Can Yayınları" seçince o yayınevinin tüm kitapları gelir. Çok yazarlı bir kitap (ör. iki yazarın ortak yazdığı) yazarlarından **herhangi biri** seçildiğinde listede çıkar.

**Why this priority**: Katalog + detay sayfasının temel gezinme eksenidir; yazar/yayınevi süzme olmadan kitapçı vitrini iş görmez. Kuzey-yıldızının ilk somut kazanımı.

**Independent Test**: Seed edilmiş katalogda facet listesi çekilir; bir yazar facet'i seçilir, dönen kitapların hepsinde o yazarın bulunduğu doğrulanır; çok yazarlı bir kitabın her iki yazar facet'inde de göründüğü doğrulanır. Aynısı yayınevi için.

**Acceptance Scenarios**:

1. **Given** çok yazarlı bir kitap (Yazar A + Yazar B), **When** Ayşe "Yazar A" facet'ini seçer, **Then** kitap listede görünür.
2. **Given** aynı kitap, **When** Ayşe "Yazar B" facet'ini seçer, **Then** kitap yine listede görünür.
3. **Given** seed edilmiş katalog, **When** yayınevi facet listesi çekilir, **Then** yalnız 4 yayınevi (`Can, İletişim, İş Kültür, YapıKredi`) ve her birinin kitap sayısı görünür.
4. **Given** ham veride "(Author)" etiketli bir yazar, **When** yazar facet'i gösterilir, **Then** etiket temizlenmiş sade ad görünür ("Yuval Harari", "Yuval Harari (Author)" değil).

---

### User Story 2 - Kitap künyesini görme (Priority: P2)

Ayşe bir kitabın detayına girince künyeyi görür: yazar(lar) ve yayınevi. İkisi de gezilebilir. Çok yazarlı kitapta tüm yazarlar listelenir. (Yazar-dışı katkıcılar — illüstratör/editör — bu kapsamda tutulmaz; bkz Assumptions.)

**Why this priority**: Künye kitapçı detay sayfasının kimliğidir; P1 süzme çalışır olduktan sonra detay zenginleşir.

**Independent Test**: Çok yazarlı bir kitabın detay verisi çekilir; yazarların **liste**, yayınevinin **tek** döndüğü doğrulanır.

**Acceptance Scenarios**:

1. **Given** iki yazarlı bir kitap, **When** detay çekilir, **Then** her iki yazar da listede, yayınevi tek döner.
2. **Given** yalnız düz isimli (rol etiketsiz) bir kitap, **When** detay çekilir, **Then** o isim tek yazar sayılır.

---

### User Story 3 - Her kitabın bir yayınevi olması (Priority: P3)

Kaynak veride yayınevi bilgisi yok denecek kadar az (1427 kitapta 2 kayıt). Vitrinin tutarlı olması için her kitaba 4 yayınevinden biri **kararlı** biçimde atanır: aynı kitap (aynı ISBN) her yeniden-üretimde **aynı** yayınevini alır.

**Why this priority**: Yayınevi facet'inin dolu ve boşluksuz olmasını sağlar; ama uydurma olduğu için en düşük öncelik.

**Independent Test**: Import iki kez çalıştırılır; her ISBN'in iki koşuda da aynı yayınevine bağlandığı doğrulanır. Hiçbir kitabın yayınevisiz kalmadığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** bir kitap (ISBN X), **When** import iki kez çalışır, **Then** iki koşuda da aynı yayınevi atanır.
2. **Given** tüm katalog, **When** yayınevi dağılımı sayılır, **Then** her kitabın tam bir yayınevisi vardır; yalnız 4 yayınevi kullanılır.

---

### Edge Cases

- **Rol etiketli çok katkıcı** ("Bill Martin Jr. (Author), Eric Carle (Illustrator)"): yalnız `(Author)` etiketliler yazar; `(Illustrator)/(Narrator)/(Editor)/(Compiler)` **atılır** (tutulmaz). "& 0 more" gibi kuyruklar atılır.
- **Düz isim, etiket yok** (1353 kayıt): tüm string tek yazar adı sayılır.
- **Kurumsal ad yazar** ("Golden Books", "Rand McNally", "Scholastic"): kişi değil ama yazar ekseninde bir ad olarak kabul edilir (kitapyurdu da kurumsal yazar gösterir).
- **Yazarı çözülemeyen / boş** ("Unknown"): tek yazar "Unknown" olarak bağlanır; kitap atılmaz.
- **Aynı yazar farklı yazımla** ("Emily Brontë" vs "Emily Bronte"): normalize edilmiş ad teklik anahtarıdır; çözülebildiği kadar birleşir, kalanı ayrı yazar kalır (bilinçli sınır).
- **Sadece yazar-dışı katkıcı** ("Tanya Emelyanova (Illustrator)"): hiç `(Author)` yoksa yazar `Unknown`; illüstratör atılır (kitap `Unknown` yazarla kalır).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Katalog, bir kitabı **birden çok yazara** bağlayabilmeli; bugünkü tek-yazar (tek referans) varsayımı kalkar.
- **FR-002**: Bir yazar seçildiğinde, o yazarın katkıda bulunduğu **tüm** kitaplar (tek ya da ortak yazarlı) listelenmeli.
- **FR-003**: Sistem, gezilebilir bir **yayınevi** kavramı tutmalı; her kitabın **tam bir** yayınevisi olmalı.
- **FR-004**: Yayınevi havuzu tam olarak 4 addan oluşmalı: `Can Yayınları`, `İletişim Yayınları`, `İş Bankası Kültür Yayınları`, `Yapı Kredi Yayınları`.
- **FR-005**: Her kitaba yayınevi ataması **kararlı** olmalı — aynı ISBN her üretimde aynı yayınevini almalı (rastgele değil, ISBN'den türetilmiş).
- **FR-006**: Yazar ve yayınevi adları gezinme/süzme facet'i olarak sunulmalı; her ikisi de dolu (boş facet üretilmemeli).
- **FR-007**: Yazar-dışı katkıcılar (illüstratör, anlatıcı, editör, derleyen) bu kapsamda **tutulmaz** — ayrıştırma sırasında yazardan ayrılıp **atılır** (yalnız ~16 kitapta var; YAGNI, veri gerçekten gelince eklenir).
- **FR-008**: Çevirmen kavramı bu kapsamda **yer almaz** (kaynak İngilizce, çeviri yok).
- **FR-009**: Ham kaynaktaki katkıcı alanının rol etiketleri (`(Author)/(Illustrator)/(Narrator)/(Editor)/(Compiler)`, "& N more" kuyrukları, "by " öneki) temizlenmeli; tüm `(Author)`'lar ayrıştırılıp çıkarılmalı, düz-isim tek yazar sayılmalı, yazar-dışı roller atılmalı.
- **FR-010**: Facet ekranlarında yazar adları rol etiketi içermemeli (kullanıcı "(Author)" gibi teknik gürültü görmemeli).
- **FR-011**: Katalogdaki bir kitabın yazar/yayınevi bilgisi değiştiğinde, vitrin (okuma tarafı) bununla tutarlı hale getirilmeli; vitrin çok-yazarı doğru yansıtmalı.
- **FR-012**: Varyant gruplaması yazar bilgisinden **bağımsız** kalmalı (aile-kodu/ürün-kimliği ekseninde; yazar değişimi gruplamayı etkilemez).
- **FR-013**: Yazarı çözülemeyen kitap atılmamalı; "Unknown" yazara bağlanmalı (mevcut davranış korunur).

### Key Entities *(include if feature involves data)*

- **Author (Yazar)**: Bir kitabın yazarı. Ad + normalize edilmiş ad (teklik anahtarı). Import sırasında get-or-create ile doğar, ad değişmez. Bir kitabın **birden çok** yazarı olabilir; bir yazarın çok kitabı olabilir (çok-çok). *Bugünkü `Brand` bunun yerini alır (rename + çok-çok'a evrim).*
- **Publisher (Yayınevi)**: Bir kitabın yayınevi. Ad + normalize edilmiş ad (teklik). Get-or-create, ad değişmez. Bir kitabın **tek** yayınevisi olur; bir yayınevinin çok kitabı olur. 4 sabit addan ISBN-kararlı atanır.
- **Book/Product (Kitap)**: Katalogdaki kitap. Yazar listesi + tek yayınevi taşır. Vitrine yansır.
- **StorefrontView (Vitrin okuma-modeli)**: Kitabın okuma tarafı temsili; yazar + yayınevi facet'lerini ve künye gösterimini besler. Çok-yazarı ve tek-yayınevini yansıtır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Katalogdaki her kitabın en az bir yazarı ve tam bir yayınevisi vardır (yayınevisiz/yazarsız kitap %0).
- **SC-002**: Yayınevi facet'i tam 4 değer gösterir; kitapların %100'ü bu 4'ten birine düşer.
- **SC-003**: Çok yazarlı bir kitap, yazarlarından her birinin facet'inde görünür (ortak-yazar kaybı %0).
- **SC-004**: Kullanıcıya gösterilen hiçbir yazar/yayınevi adında rol etiketi ("(Author)" vb.) veya "& N more" kuyruğu görünmez.
- **SC-005**: Import iki kez çalıştırıldığında ISBN→yayınevi eşlemesi %100 aynıdır (kararlılık).
- **SC-006**: Yazar-dışı roller (illüstratör/editör/anlatıcı) yazar listesine **sızmaz** — çok-katkıcı ham kayıtta yalnız `(Author)`'lar yazar olur.

## Assumptions

- **Sıfırdan seed**: Ürün silme yok, DB sıfırdan seed ediliyor (016 kuralı). Bu yüzden mevcut `Brand` verisi için **taşıma (migration) gerekmez** — katalog yeniden seed edilir.
- **Veri temizliği build-zamanı**: Rol-etiketi ayrıştırma `shape_books.py` (İş1) içinde yapılır; `books.json` yeniden üretilir. Çalışma-zamanı seeder temizlenmiş veriyi okur.
- **Kararlı yayınevi**: ISBN'den türetilmiş deterministik seçim (ör. hash mod 4). Canlı kur/gerçek yayınevi eşlemesi yok — uydurma, sonraki fazlar (ML fiyat vb.) bunu değiştirebilir.
- **Kurumsal yazar kabul**: "Golden Books" gibi kurumsal adlar yazar ekseninde ad olarak kabul; kişi/kurum ayrımı yapılmaz.
- **Normalize teklik**: Yazar/yayınevi tekliği normalize edilmiş ada dayanır (mevcut `Brand` normalizasyon kalıbı sürdürülür); aksanlı/yazım farkları çözülebildiği kadar birleşir.
- **Yazar-dışı katkıcı — bilinçli düşürüldü (YAGNI)**: illüstratör/editör/anlatıcı yalnız ~16/1427 kitapta (%1) var. Contributor kavramını 5 katmana (shape/VO/event/read-model/DTO) taşımak %1 için erken; ayrıştırmada atılır. Katalog gerçek kitapçı verisiyle büyüyünce geri eklenir.
- **Kapsam dışı**: Frontend katalog/detay **sayfası** (SPA/Blazor kararı açık); favoriler, alışveriş listesi, fiyat alarmı, son-gezdiklerim (kuzey-yıldızının sonraki adımları). Çevirmen ekseni.
- **Bağımlılık**: [[book-author-publisher-model]] tasarım kararlarına ve 051 book-import omurgasına dayanır; Catalog→Storefront `ProductChangedEvent` mevcut kanalı kullanılır.