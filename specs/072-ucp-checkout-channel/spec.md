# Feature Specification: UCP Checkout Kanalı

**Feature Branch**: `072-ucp-checkout-channel`

**Created**: 2026-09-10

**Status**: Draft

**Input**: User description: "UCP checkout kanalı"

**Artefakt kademesi**: **Tam** — yeni Bounded Context (`ucp`, kendi DB'si), yeni endpoint kontratları
(checkout session + keşif + webhook), yeni servisler-arası akış (Order'a already-captured dış-sipariş),
belirsizlik var. Küçük kademe koşulları bozuluyor → tam akış.

## Bağlam (neden bu feature)

Mağaza, dış AI platformlarının/agent'larının (ChatGPT, Gemini vb. arkasındaki ticaret istemcileri)
mağazadan **kendi arayüzlerinden** alışveriş tamamlayabilmesi için standart bir ticaret kapısı açar.
Seçilen standart **UCP (Universal Commerce Protocol)** — kanonik spec `Universal-Commerce-Protocol/ucp`.
Önceki ACP denemesi (071) terk edildi; UCP'nin OAuth-native kimlik modeli mağazanın mevcut OpenIddict +
DCR altyapısına doğrudan oturduğu, standardın gelecekte yaygınlaşacağı öngörüsüyle UCP seçildi.

UCP **ayrı bir checkout kanalıdır**: mağazanın mevcut web/saga checkout akışının içine girmez, onunla
paralel yaşar. Kanal yalnız en sonda, ödeme tahsil edildikten sonra, siparişi mevcut sipariş borusuna
"zaten tahsil edildi" olarak devreder.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Dış platform UCP ile satın alma tamamlar (Priority: P1)

Dış bir platform, müşteri adına mağazada bir checkout session açar, ürün ekler, alıcı/teslimat bilgisi
verir ve session'ı tamamlar. Tamamlanma anında ödeme mağazanın ödeme sağlayıcısı üzerinden tahsil edilir
ve mağazada ödemesi alınmış bir sipariş oluşur. Bu, feature'ın çekirdek değeridir.

**Why this priority**: Kanalın var oluş nedeni bu; keşif ve webhook bu akışa hizmet eder. Bu tek başına
uçtan uca satın almayı sağlar → MVP.

**Independent Test**: Simülatörden session aç → kalem ekle → alıcı/teslimat gir → complete. Sonuç:
mağazada tek bir ödemeli sipariş + ödeme sağlayıcıda karşılık gelen tahsilat kaydı.

**Acceptance Scenarios**:

1. **Given** geçerli yetki ve ürün, **When** platform session açıp kalem ekler, **Then** session `incomplete` durumda totals ile döner.
2. **Given** alıcı + teslimat girilmiş session, **When** platform complete çağırır, **Then** ödeme tahsil edilir ve session `completed` + sipariş referansı döner.
3. **Given** ödeme tahsili başarısız, **When** platform complete çağırır, **Then** session `completed` OLMAZ, açıklayıcı hata mesajı döner, sipariş oluşmaz.
4. **Given** tamamlanmış bir session, **When** aynı complete tekrar gelir, **Then** yeni sipariş oluşmaz (idempotent), mevcut sonuç döner.

---

### User Story 2 - Dış platform mağazayı ve ürünleri keşfeder (Priority: P2)

Platform, mağazanın hangi UCP yeteneklerini desteklediğini, hangi ödeme yöntemlerini kabul ettiğini ve
imza doğrulama anahtarlarını standart bir keşif adresinden öğrenir; ardından satılabilir ürünleri
kimlikle getirir ya da arar.

**Why this priority**: Satın alma akışını başlatmak için platformun kapıyı ve ürünleri tanıması gerekir;
ama US1 sabit bir ürün kimliğiyle de gösterilebildiğinden P2.

**Independent Test**: Keşif adresini çek → yetenek/ödeme/anahtar listesini doğrula. Ürün ara ve kimlikle
getir → satılabilir ürün alanları döner.

**Acceptance Scenarios**:

1. **Given** mağaza ayakta, **When** platform keşif profilini ister, **Then** desteklenen yetenekler, ödeme yöntemleri ve public imza anahtarları döner.
2. **Given** katalogda satılabilir ürün, **When** platform arama/gete çağırır, **Then** ürünün başlık/fiyat/uygunluk bilgisi döner.

---

### User Story 3 - Mağaza sipariş olaylarını platforma bildirir (Priority: P3)

Bir UCP siparişinin durumu değiştiğinde (onaylandı, iptal edildi) mağaza, platforma imzalı bir bildirim
gönderir; teslim edilemezse yeniden dener.

**Why this priority**: Satın alma US1 ile tamamlanır; bildirim tamamlayıcı deneyimdir, çekirdek satın
almayı bloke etmez → P3.

**Independent Test**: Bir UCP siparişinin durumunu değiştir → simülatörün bildirim alıcısı imzalı olayı
alır ve imzayı doğrular; ilk teslim başarısızsa yeniden deneme gözlenir.

**Acceptance Scenarios**:

1. **Given** tamamlanmış UCP siparişi, **When** sipariş onaylanır, **Then** platform imzalı `order confirmed` bildirimi alır.
2. **Given** UCP siparişi, **When** iptal edilir, **Then** platform imzalı `order canceled` bildirimi alır.
3. **Given** bildirim teslimi başarısız, **When** yeniden deneme penceresi geçer, **Then** sınırlı sayıda tekrar denenir.

---

### User Story 4 - Dış agent yetki + istek bütünlüğü (Priority: P2)

Kanala erişen dış agent, mağazanın kimlik sunucusundan yetki alır (checkout scope'u) ve gönderdiği
isteklerin bütünlüğü doğrulanır.

**Why this priority**: Güvenlik çekirdeği; imza tam uygulanır ama zorlaması opsiyonel bayrakla
(sandbox varsayılanı kapalı) olduğundan çekirdek satın almayı bloke etmez → P2.

**Independent Test**: Yetkisiz istek reddedilir; geçerli scope'lu istek kabul edilir. Bütünlük doğrulaması
açıkken bozuk/eksik imzalı istek reddedilir.

**Acceptance Scenarios**:

1. **Given** scope'suz çağrı, **When** korumalı uca gelir, **Then** reddedilir (yetkisiz).
2. **Given** geçerli checkout scope'lu çağrı, **When** korumalı uca gelir, **Then** kabul edilir.

---

### Edge Cases

- Session süresi (varsayılan 6 saat) dolduktan sonra complete gelirse reddedilir.
- `requires_escalation` durumu: platforma devir adresi (continue_url) verilmeden bu duruma geçilmez.
- Complete anında stok yetersizse sipariş oluşmaz, açıklayıcı hata döner (ödeme tahsil edilmişse telafi edilir).
- Update, kalem listesini **tam değişimle** günceller (kısmi ekleme/çıkarma değil); tüketici tam listeyi gönderir.
- Bütünlük doğrulaması açıkken imza eksik/bozuk istek reddedilir; kapalıyken kaydedilir ama akış bloke olmaz.
- Geçersiz/süresi dolmuş/uygunsuz indirim kodu: uygulanmaz, session hataya düşmez, açıklayıcı mesaj döner.
- Teslimat adresine uygun kargo seçeneği yoksa: complete edilebilir duruma geçilmez, eksik açıkça bildirilir.
- İndirim/kargo değişince toplamlar yeniden hesaplanır; complete anında toplam yeniden doğrulanır (stale toplamla tahsilat yok).
- İç web checkout akışı UCP'den etkilenmemeli (regresyon yok).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, mağazanın UCP yeteneklerini, kabul edilen ödeme yöntemlerini ve public imza anahtarlarını yayınlayan bir **keşif profili** sunmalı.
- **FR-002**: Sistem, dış agent'ı mağazanın kimlik sunucusu üzerinden **checkout scope'u** ile yetkilendirmeli; yetkisiz erişimi reddetmeli.
- **FR-003**: Sistem, verilen ürün kalemleriyle bir **checkout session oluşturmalı** (para birimi TRY, toplamlar, yasal bağlantılar, başlangıç durumu `incomplete`).
- **FR-004**: Sistem, session'ı **güncellemeli** — kalem listesi tam değişim, alıcı ve teslimat bilgisi; toplamları yeniden hesaplamalı.
- **FR-005**: Sistem, session **durum makinesini** yürütmeli: `incomplete → ready_for_complete → complete_in_progress → completed | canceled`; devir gereken hallerde `requires_escalation` + devir adresi.
- **FR-006**: Sistem, complete anında **ödemeyi mağazanın ödeme sağlayıcısı üzerinden tahsil etmeli** (sandbox ortamı); başarılıysa ödemesi-alınmış sipariş üretmeli.
- **FR-007**: Sistem, sipariş oluşturmayı mevcut **sipariş borusuna "zaten tahsil edildi" olarak devretmeli**; iç checkout saga'sının ödeme adımı atlanmalı; web checkout akışı değişmemeli.
- **FR-008**: Sistem, süresi dolmuş (varsayılan 6 saat) session'ın complete edilmesini reddetmeli.
- **FR-009**: Complete işlemi **idempotent** olmalı; aynı session için tekrarlanan complete yeni sipariş üretmemeli.
- **FR-010**: Sistem, sipariş olaylarını (onaylandı, iptal edildi) platforma **imzalı bildirimle** göndermeli; teslim başarısızsa sınırlı yeniden deneme uygulamalı.
- **FR-011**: Sistem, gelen isteklerin bütünlüğünü **RFC 9421 HTTP Message Signatures** (+ RFC 9530 Content-Digest gövde özeti) ile doğrulamalı; gönderenin public anahtarı profilinden (JWKS, `kid` ile) çözülmeli. Zorlama **opsiyonel bir bayrakla** kontrol edilmeli: açıkken imzasız/bozuk istek reddedilir; kapalıyken (sandbox varsayılanı) imza varsa doğrulanır ama akış bloke olmaz.
- **FR-012**: Sistem, satılabilir ürünleri **kimlikle getirme ve arama** ile keşfe açmalı; bu, kanalın kendi okuma projeksiyonundan sunulmalı.
- **FR-013**: Kanal, **kendi izole veri deposuna** sahip olmalı (session durumu + katalog projeksiyonu); başka BC'nin veritabanına/aggregate'ine dokunmamalı.
- **FR-014**: Uçtan uca akışı göstermek için **platform rolünü canlandıran bir simülatör** sağlanmalı; harici bir AI istemcisinden (ör. masaüstü asistan) sürülebilmeli.
- **FR-015**: Kapsam **tam UCP alışverişidir**: checkout yeteneği + **fulfillment** uzantısı (kargo seçenekleri/adres) + **discount** uzantısı (indirim kodu). Üçü de bu feature'da.
- **FR-016**: Mağazanın kabul ettiği ödeme yöntemi profilde ilan edilmeli ve complete akışında seçilen yöntem bu ilanla tutarlı olmalı.
- **FR-017**: Sistem, **fulfillment (teslimat) uzantısını** desteklemeli: session'a teslimat adresi girilince uygun kargo seçenekleri sunulmalı, seçilen seçeneğin bedeli toplamlara yansıtılmalı.
- **FR-018**: Sistem, **discount (indirim) uzantısını** desteklemeli: geçerli indirim kodu uygulanınca indirim toplamlara yansıtılmalı; geçersiz/uygunsuz kod açıklayıcı mesajla reddedilmeli.

### Key Entities *(include if feature involves data)*

- **UCP Checkout Session**: Bir dış-kanal satın alma niyetinin durumu — kimlik, durum, para birimi, kalemler, toplamlar, alıcı, teslimat, son-kullanma, yasal bağlantılar, sipariş referansı.
- **Line Item (kanal görünümü)**: Bir ürün kaleminin sade görünümü — ürün referansı, adet, fiyat. (Catalog'un zengin ürün modelinden ayrı; İlke I.)
- **Katalog Projeksiyon Kalemi**: Keşif/arama için satılabilir ürün anlık görüntüsü — kimlik, başlık, fiyat, uygunluk. Ürün/stok olaylarından beslenir.
- **Ödeme Yöntemi İlanı**: Profilde ilan edilen, mağazanın kabul ettiği ödeme yöntemi tanımı.
- **Kargo Seçeneği (fulfillment)**: Teslimat adresine göre sunulan, ad + bedel + tahmini süre taşıyan seçenek.
- **İndirim (discount)**: Session'a uygulanan indirim — kod, tutar/oran, toplamlara etkisi.
- **İmza Anahtarı (public)**: Keşif profilinde yayınlanan, hem giden bildirim imzasını hem gelen istek doğrulamasını (RFC 9421) besleyen doğrulama anahtar(lar)ı.
- **Giden Bildirim Teslimi**: Bir sipariş olayının platforma teslim durumu — olay, hedef, imza, yeniden deneme durumu.
- **Dış Sipariş (already-captured)**: Tamamlanmış bir session'dan üretilen, ödemesi önceden alınmış sipariş kaydı.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Bir dış platform, keşiften siparişe kadar tam akışı simülatör üzerinden 2 dakikanın altında tamamlayabilir.
- **SC-002**: Tamamlanan her session **tam olarak bir** sipariş üretir; tekrarlanan complete ek sipariş üretmez (%100 idempotent).
- **SC-003**: Ödeme sandbox ortamında gerçekçi biçimde tahsil edilir; ödeme başarısız olduğunda hiçbir sipariş oluşmaz (%100).
- **SC-004**: Bir siparişin durumu değiştiğinde platform, en çok 3 teslim denemesi içinde imzalı bildirimi alır.
- **SC-005**: UCP kanalının eklenmesi mevcut web checkout akışını bozmaz — mevcut checkout regresyon testleri geçmeye devam eder.
- **SC-006**: Kanalın döndürdüğü checkout session yanıtı, kanonik UCP checkout şemasına uyar (şema doğrulaması geçer).

## Assumptions

- Ödeme sağlayıcısı (PaymentGateway) ayrı bir depodadır ve bu feature onu **değiştirmez**; mevcut tahsilat yolu (chat charge şablonu) yeniden kullanılır.
- Ödeme sağlayıcısı **sandbox ortamına** bağlıdır ve çalışan test anahtarı vardır; gerçek para hareketi olmaz, 3DS gerektirmeyen test kartı kullanılır. (**Doğrulanacak dış bağımlılık.**)
- UCP kanalı, mevcut iç checkout saga'sının ve web akışının **ortasına girmez**; paralel kanaldır ve yalnız already-captured devir noktasında sipariş borusuna değer.
- UCP spec sürümü, işin başlangıcındaki güncel yayına sabitlenir; sürüm sıçraması ayrı bir iştir.
- Yetkilendirme scope'u standardın önerdiği checkout scope'una hizalanır (interop için).
- Para birimi TRY; toplamlar mağaza tarafından belirlenir.
- Session son-kullanma süresi belirtilmezse varsayılan 6 saattir (UCP varsayılanı).
- İstek bütünlüğü imzası (RFC 9421) tam uygulanır ama zorlaması opsiyonel bayrakladır; sandbox varsayılanı kapalı.
- Kapsam tam UCP alışverişidir (checkout + fulfillment + discount); hiçbir parça sonraki feature'a ertelenmez.
- Kargo tarifesi/indirim kuralları için basit/mevcut mağaza mantığı kullanılır; karmaşık promosyon motoru kapsam dışı.

## Dependencies

- **Identity.Server**: dış agent yetkisi (checkout scope) + dış agent kaydı (DCR).
- **Order**: already-captured dış-sipariş oluşturma noktası (mevcut chat/AlreadyCaptured deseni).
- **PaymentGateway** (dış depo): tahsilat; bu feature'da değişmez.
- **Catalog / Storefront olayları**: katalog projeksiyonunu besleyen ürün/stok olayları.