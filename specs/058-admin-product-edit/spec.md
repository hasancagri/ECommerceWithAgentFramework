# Feature Specification: Admin Ürün Düzenleme (Edit-Only)

**Feature Branch**: `058-admin-product-edit`

**Created**: 2026-09-02

**Status**: Draft

**Input**: User description: "Admin ürün düzenleme (edit-only). WebApp /Admin altında iki ekran: ürün listesi (sayfalama + ad/ISBN arama, draft dahil) + düzenleme formu (çekirdek künye + fiyat + mutlak stok + yayın anahtarı). Kitapyurdu standardı UI. Oluşturma/silme kapsam dışı."

**Artefakt kademesi**: Küçük (`spec.md` + `tasks.md`) — bilinçli seçim (tasarım onayında kullanıcı kararı):
yeni aggregate/integration event YOK; yeni endpoint'ler mevcut aggregate davranışlarının ince yüzeyleri.
Tek yeni kalıcı kayıt fiyat-geçmişi document'ı (aggregate değil, BC-içi append-only audit). "Yeni
endpoint/tablo" maddelerine rağmen Tam kademe töreni bu gerekçeyle atlandı.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Künye + fiyat düzenleme (Priority: P1)

Mağaza sahibi (admin) bir kitabın bilgisini düzeltmek ister: "Nutuk"u admin ürün listesinde adıyla arar,
düzenleme ekranını açar; adı, açıklamaları, fiyatı, yazarları, yayınevini, kategoriyi veya görseli
değiştirip kaydeder. Değişiklik vitrinde (ürün detay + listeler) kendiliğinden görünür.

**Why this priority**: Feature'ın varlık sebebi — 050 pivot sonrası ürünler mağazanın, ama düzeltme
yüzeyi hiç yok. Fiyat değişikliği (G2 fiyat olayları backlog'u) bu yüzeye dayanacak.

**Independent Test**: Admin login → listede ara → formda fiyat değiştir → kaydet → vitrindeki detay
sayfasında yeni fiyatı gör. Tek başına teslim edilse bile değer üretir.

**Acceptance Scenarios**:

1. **Given** admin girişli ve ürün listesi açık, **When** "Nutuk" aranır, **Then** eşleşen ürünler kapak + ad + yazar + fiyat + yayın durumu ile listelenir (sayfalama çalışır).
2. **Given** düzenleme formu açık, **When** fiyat 95 yapılıp kaydedilir, **Then** başarı geri bildirimi görünür ve vitrin detay sayfası yeni fiyatı gösterir.
3. **Given** düzenleme formu açık, **When** yazar listesinden mevcut bir yazar seçilir ya da listede olmayan yeni bir ad yazılır, **Then** kayıt sonrası ürün o yazar(lar)a bağlanır; yeni ad sistemde tek örnek olarak doğar (get-or-create).
4. **Given** düzenleme formu açık, **When** yayınevi değiştirilir veya kategori ağaçtan başka bir dala taşınır, **Then** vitrindeki künye ve kategori filtreleri yeni değeri yansıtır.
5. **Given** düzenleme formu açık, **When** ad boş bırakılıp kaydedilmek istenir, **Then** kayıt reddedilir ve alan bazlı hata mesajı görünür.
6. **Given** ürünün fiyatı daha önce hiç değişmemiş, **When** admin fiyatı 80'den 95'e çeker, **Then** düzenleme ekranındaki fiyat geçmişi ilk (import) fiyatı ve bu değişikliği (eski→yeni + zaman) sırayla gösterir.

---

### User Story 2 - Stok düzeltme (Priority: P2)

Admin, deposunda saydığı gerçek adedi sisteme işler: düzenleme ekranındaki stok bölümünde mevcut adedi
görür ve "stok 50 olsun" der; vitrindeki stok görünümü buna uyar.

**Why this priority**: Stok gerçeği satışın ön şartı; ama künye düzenlemeden bağımsız çalışabilir ve
mevcut elle artır/azalt API'lerinin üstüne gelen ikincil bir yüzeydir.

**Independent Test**: Admin formda stok 50 girer → kaydeder → vitrin detayında stok durumu güncellenir;
Checkout o üründen 50 adede kadar satışa izin verir.

**Acceptance Scenarios**:

1. **Given** ürünün mevcut stoğu 12, **When** admin mutlak değer 50 girip kaydeder, **Then** stok 50 olur ve formda yeni değer görünür.
2. **Given** stok bölümü açık, **When** negatif değer girilir, **Then** kayıt reddedilir ve hata mesajı görünür.
3. **Given** stok 0 yapıldı, **When** müşteri vitrinde ürüne bakar, **Then** ürün stoksuz görünür ve sepete eklense bile checkout stok gerçeğine takılır.

---

### User Story 3 - Yayına al / yayından kaldır (Priority: P3)

Admin bir kitabı geçici olarak satıştan çeker (yayından kaldırır) ya da fiyatı girilmemiş draft bir
kitabı fiyatlayıp yayına alır. Yayında olmayan ürün vitrine çıkmaz ama admin listesinde görünür kalır.

**Why this priority**: Düzeltme akışının tamamlayıcısı; import'tan fiyatsız kalıp draft düşen kitapları
satışa açmanın tek yolu. Ana düzenleme akışı olmadan tek başına anlamı sınırlı.

**Independent Test**: Draft (fiyatsız) bir ürüne fiyat verip yayına al → vitrinde görünür; yayından
kaldır → vitrinden düşer, admin listesinde "yayında değil" olarak kalır.

**Acceptance Scenarios**:

1. **Given** fiyatı 0 olan draft ürün, **When** admin fiyat girmeden yayına almayı dener, **Then** işlem reddedilir ve "fiyat gerekli" kuralı kullanıcıya açıklanır.
2. **Given** yayında bir ürün, **When** admin yayından kaldırır, **Then** ürün vitrin listelerinde ve aramada görünmez olur; admin listesinde durumu "yayında değil" gösterilir.
3. **Given** yayında olmayan, fiyatlı bir ürün, **When** admin yayına alır, **Then** ürün vitrinde tekrar görünür.

---

### Edge Cases

- Arama hiçbir sonuç döndürmezse liste boş-durum mesajı gösterir; sayfalama sınır dışı sayfa istenirse son geçerli sayfaya düşer.
- Var olmayan/silinmiş kimlikle düzenleme ekranı açılırsa "bulunamadı" sayfası gösterilir, form açılmaz.
- Yazar listesi boş bırakılırsa (hiç yazar seçilmemiş) kayıt reddedilir — kitap en az bir yazar taşır.
- Aynı ürünü iki oturum aynı anda düzenlerse son kaydeden kazanır (tek yönetici varsayımı; kilitleme yok).
- Yetkisiz (admin olmayan) kullanıcı admin sayfalarına veya düzenleme uçlarına erişemez; giriş/yetki hatası alır.
- Stok düzeltme anında o üründen checkout geçiyorsa iki yazma da geçerli sırayla uygulanır; negatif sonuç doğuran mutlak set reddedilir.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Admin, tüm ürünleri (yayında + draft) sayfalanmış listede görebilmeli; her satır kapak küçük görseli, ad, yazar(lar), fiyat ve yayın durumunu gösterir.
- **FR-002**: Liste, ürün adına ve ISBN'e göre aranabilmeli; arama draft ürünleri de kapsar.
- **FR-003**: Admin, tek ürünün düzenleme ekranında şu alanları değiştirebilmeli: ad, kısa açıklama, tam açıklama, fiyat, yazarlar, yayınevi, kategori, görsel adresi.
- **FR-004**: Yazar alanı çoklu seçim olmalı: mevcut yazarlardan seçilir; listede olmayan ad yazılırsa sistemde tek örnek olarak oluşturulup bağlanır (get-or-create). En az bir yazar zorunlu.
- **FR-005**: Yayınevi alanı tek seçim ve zorunlu olmalı; yazarla aynı seç-veya-yarat davranışını izler.
- **FR-006**: Kategori, mevcut kategori ağacından seçilmeli; serbest metin kategori girilemez.
- **FR-007**: Admin, ürünün stok adedini mutlak değer olarak ayarlayabilmeli ("stok N olsun"); mevcut adet formda görünür; negatif değer reddedilir.
- **FR-008**: Admin, ürünü yayına alabilmeli/yayından kaldırabilmeli; fiyatı olmayan ürün yayına alınamaz (mevcut iş kuralı korunur).
- **FR-009**: Kaydedilen her değişiklik (künye, fiyat, yayın durumu, stok) vitrine mevcut yayın mekanizmasıyla yansımalı; vitrin tarafında ek elle işlem gerekmez.
- **FR-010**: Admin ekranları ve tüm yazma uçları yalnız katalog/stok yazma yetkisi taşıyanlara açık olmalı (mevcut scope modeli; bu feature'da yeni rol tanımlanmaz).
- **FR-011**: Geçersiz girdi (boş ad, boş yazar, negatif fiyat/stok) alan bazlı, anlaşılır hata mesajıyla reddedilmeli; başarılı kayıt kullanıcıya doğrulanmalı.
- **FR-012**: Ekranlar sitenin kitapyurdu-hizalı görsel dilini kullanmalı; formdaki künye alan düzeni, vitrin detay sayfasındaki künye sunumuyla aynı adlandırmayı taşımalı.
- **FR-013**: Her fiyat değişikliği kalıcı kayda geçmeli (eski fiyat, yeni fiyat, zaman); ürünün import anındaki ilk fiyatı da ilk kayıttır. Fiyatı değiştirmeyen kayıtlar geçmişe satır düşürmez.
- **FR-014**: Admin, düzenleme ekranında ürünün fiyat geçmişini baştan sona kronolojik listeleyebilmeli.

### Key Entities

- **Ürün (künye)**: Düzenlemenin öznesi — ad, açıklamalar, fiyat, yazarlar, yayınevi, kategori, görsel, yayın durumu. Catalog'un zengin modeli; vitrindeki görünümü ayrı read-model.
- **Yazar / Yayınevi**: Ada göre tekil sözlük kayıtları; yalnız seç-veya-yarat, bu feature'da yeniden adlandırma yok.
- **Kategori**: Mevcut ağaç; düzenlemede yalnız seçim kaynağı.
- **Stok kaydı**: Ürün başına eldeki adet (OnHand); bu feature yalnız mutlak düzeltme yüzeyi ekler.
- **Fiyat geçmişi kaydı**: Ürün başına append-only fiyat değişim satırları (eski, yeni, zaman); ilk satır import fiyatı. Fiyatın sahibi bağlamda (Catalog) yaşar; silinmez/değiştirilmez.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin, bir kitabın fiyat değişikliğini (bul → düzenle → kaydet → vitrinde doğrula) 1 dakikanın altında tamamlayabilir.
- **SC-002**: Düzenlenen künye/fiyat/stok, kayıttan sonra 10 saniye içinde vitrinde (detay + liste) doğru görünür.
- **SC-003**: Ad veya ISBN ile arayan admin, aradığı kitabı ilk sonuç sayfasında bulur (tam ISBN eşleşmesi tek sonuç döndürür).
- **SC-004**: Yetkisiz erişim denemelerinin %100'ü engellenir; admin olmayan hiçbir kullanıcı düzenleme ekranını göremez ve yazma yapamaz.
- **SC-005**: Fiyatsız ürünü yayına alma denemelerinin %100'ü, nedeni açıklanan bir hata ile reddedilir.
- **SC-006**: Fiyat değişikliklerinin %100'ü geçmişte görünür — bir ürünün geçmiş satır sayısı, ilk fiyat + o güne kadarki gerçek fiyat değişikliği sayısına her an eşittir.

## Assumptions

- Ürün oluşturma ve silme kapsam DIŞI (silme yok kuralı — 016 — sürer); etiket, özellik (specification), SEO ve ölçü düzenleme de bu feature'ın dışında, sonraki dilime kalır.
- Yeni rol tanımlanmaz; ekranlar mevcut katalog/stok yazma yetkileriyle korunur. "Katalog sorumlusu" rolü ayrı küçük feature (backlog).
- Author/Publisher yeniden adlandırma yok (mevcut modelde ad değişmez); yanlış yazılmış yazar adı düzeltme ayrı iş.
- Eşzamanlı düzenleme nadir (tek yönetici); son-yazan-kazanır yeterli, iyimser kilitleme kurulmaz.
- Vitrine yansıma mevcut yayın/abonelik akışıyla olur; yeni servisler-arası sözleşme eklenmez. Draft ürünler vitrinde zaten yoktur; admin listesi ana kaynaktan (Catalog) okur.
- Fiyat geçmişi bu feature'da yalnız admin yüzüne açılır; müşteri-yüzü fiyat grafiği/rozet (kitapyurdu görünümü) G2 fiyat-olayları feature'ının işi — veri o güne birikmiş olur.
- Stok düzeltmesi Stock BC'ye yeni bir komut yüzeyi ekler (mutlak ayar); bu Stock'un domain sürecine dokunduğundan Stock `FLOW.md` aynı PR'da güncellenir (İLKE VII).
- UI kitapyurdu standardı: sitenin oturmuş kitapyurdu-hizalı düzeni (tipografi/renk/kart dili); admin ayrı "ham tablo" estetiğine kaçmaz.