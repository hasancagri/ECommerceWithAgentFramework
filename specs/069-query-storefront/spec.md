# Feature Specification: Query Storefront — Tek Serbest-Sorgu Kapısı

**Feature Branch**: `069-query-storefront`

**Created**: 2026-09-07

**Status**: Draft

**Input**: User description: "Storefront agent sorgu yüzeyi tek kapıya iner (query_storefront):
müşterinin sorabileceği HER isteği karşılayan tek endpoint; asistan sorguyu kendisi kurar, uygulama
yalnız sonucu bastırır. Parametrik arama + benzer-kitap tool'ları tam ikameyle kaldırılır. Brainstorm +
spike kararları kilitli (bkz Assumptions)."

**Kademe**: Tam — mevcut arama sözleşmesi tamamen değişir (iki tool silinir, yeni tek tool), asistan
davranış sözleşmesi baştan yazılır, güvenlik sınırı yeniden tanımlanır. 068 bu kararla İPTAL edildi.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Hiçbir soru "buna sığmıyor" duvarına çarpmaz (Priority: P1)

Müşteri sohbette aklına gelen HER mağaza sorusunu sorar — basit filtre ("Wells'in 300 TL altı stoktaki
kitapları"), sıralama/uç değer ("en ucuz 5 bilim kurgu"), istatistik ("kategori başına ortalama fiyat",
"en çok kitabı olan yazar kim"), karşılaştırma ("Tolkien mi King mi daha çok kitaba sahip"),
alanlar-arası VEYA ("fantastik kategorisinden VEYA Le Guin'den"), puan şartı ("4 üstü puanlı"),
özellik/varyant ("bunun ciltli hali var mı"). Asistan soruyu tek sorgu yüzeyinden yanıtlar; bugünkü
"parametreye sığmadı, kibarca reddet" sınıfı tamamen ortadan kalkar.

**Why this priority**: Kuzey-yıldızının son halkası — "metin üzerinden TAM keşif" ancak soru uzayının
tamamı karşılanınca gerçek olur. Kullanıcının bu feature'daki asıl niyeti bu cümledir: "kullanıcının
isteklerini tam olarak karşılayacak bir endpoint".

**Independent Test**: Eski yüzeyin reddettiği temsilî sorular (aggregation, karşılaştırma, cross-facet
VEYA) chat'e sorulur; hepsinin katalog verisiyle tutarlı, uydurmasız yanıtlandığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** satıştaki katalog, **When** müşteri bir istatistik/karşılaştırma sorusu sorar ("hangi
   yayınevinin ortalaması en ucuz"), **Then** asistan gerçek veriden hesaplanmış yanıtı verir.
2. **Given** alanlar-arası VEYA isteği, **When** soru tek cümlede gelir, **Then** yanıt tek akışta
   döner (asistanın birden çok arama yapıp elle birleştirmesi gerekmez).
3. **Given** yanıtlanamayan bir soru sınıfı (ör. satış adedi — veri yok), **When** sorulursa,
   **Then** asistan verinin mağazada tutulmadığını dürüstçe söyler; uydurmaz.

---

### User Story 2 - Temalı arama ve benzerlik aynı kapıdan (Priority: P1)

"Kışın okunacak sürükleyici bilim kurgu, 300 TL altı" gibi temalı istekler ve "buna benzer başka ne
var" istekleri de aynı tek yüzeyden karşılanır — üstelik benzerlik artık filtreyle birleşebilir
("buna benzer AMA 200 TL altı ve stokta") ki bu eski yüzeyde hiç yoktu.

**Why this priority**: 067'de canlıya alınan anlamsal yetenek korunmak zorunda (gerileme kabul
edilmez) + filtreli-benzerlik yeni kazanım.

**Independent Test**: 067 quickstart'ının S3/S5 senaryoları yeni yüzeyde tekrarlanır (temalı arama
kısıtlara uyar; alakasız sorguda "bulunamadı"; benzer listesi kendisi-hariç) + filtreli-benzerlik
yeni senaryosu eklenir.

**Acceptance Scenarios**:

1. **Given** temalı istek + yapısal kısıt, **When** arama koşar, **Then** dönen her sonuç kısıtlara
   uyar ve anlamca ilgililer önde gelir; yeterince ilgili sonuç yoksa dürüst "bulunamadı".
2. **Given** bir kitaba benzerlik isteği + fiyat/stok kısıtı, **When** koşar, **Then** benzerler
   kısıtları da sağlar; referans kitap sonuçta görünmez.

---

### User Story 3 - Sorgu yüzeyi güvenli ve gözlemlenebilir (Priority: P1)

Asistanın kurduğu hiçbir sorgu; veri değiştiremez, izinli vitrin yüzeyi dışına (ör. kullanıcıların
satın-alma kayıtları) erişemez, sınırsız sonuç çekemez ya da sistemi kilitleyemez. Kurulan her sorgu
ve sonucu, sonradan incelenebilecek şekilde iz bırakır.

**Why this priority**: Serbest-sorgu kapısının ön şartı; bu güvenceler olmadan US1 yayına çıkamaz.

**Independent Test**: Kötücül/aykırı sorgu denemeleri (veri değiştirme, izinsiz alan, devasa sonuç,
sonsuz sorgu) tek tek gönderilir; hepsinin çalışmadan reddedildiği ve izlendiği doğrulanır.

**Acceptance Scenarios**:

1. **Given** veri değiştiren ya da izinli yüzey dışına çıkan bir sorgu, **When** kapıya gelirse,
   **Then** ÇALIŞTIRILMADAN reddedilir; asistan düzeltip yeniden deneyebilir.
2. **Given** herhangi bir başarılı/başarısız sorgu, **When** koşarsa, **Then** sorgu metni + sonuç
   sayısı kayıt altına alınır (kalite/güvenlik incelemesi için).
3. **Given** yayından kaldırılmış bir ürün, **When** herhangi bir sorgu koşarsa, **Then** o ürün
   hiçbir yanıtta görünmez (izinli yüzey yalnız satılabilir ürünleri içerir).

---

### Edge Cases

- Asistan bozuk/geçersiz sorgu kurarsa: kapı açıklayıcı hatayla reddeder, asistan sınırlı sayıda
  düzeltme denemesi yapar; yine olmazsa kullanıcıya dürüst "bu soruyu şu an yanıtlayamadım" der.
- Sonuç kümesi sayfa tavanından büyükse: asistan toplamı söyler, devamını isteme akışı sunar
  (aynı sorgunun sonraki dilimi).
- "Çok satan/bestseller" soruları: satış verisi vitrin yüzeyinde YOK — dürüst sınır; ayrı feature.
- "Yeni gelenler": ekleniş tarihi yaklaşıktır (kayıt güncellenme zamanı) — kabul edilen yaklaşıklık.
- Boş katalog dilimi (ör. kısıt hiç ürün bırakmadı): boş sonuç + dürüst ifade; hata değil.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Storefront'un asistan-sorgu yüzeyi TEK kapıya inmelidir; mevcut parametrik arama ve
  benzer-kitap yüzeyleri tam ikameyle kaldırılır (kırıcı değişiklik kabul).
- **FR-002**: Tek kapı; filtre, sıralama, uç-değer, sayım/istatistik, karşılaştırma, alanlar-arası
  VEYA/HARİÇ, özellik/varyant, puan şartı ve sayfalama içeren istekleri karşılayabilmelidir.
- **FR-003**: Temalı (anlamsal) arama ve ürün-benzerliği aynı kapıdan sürmeli; anlamsal yakınlık
  hesabının girdisini asistan üretMEZ (metni iletir, dönüşümü sistem yapar); alaka eşiği ve
  "bulunamadı" dürüstlüğü (067) korunur; benzerlik yapısal kısıtlarla birleşebilir.
- **FR-004**: Kapı yalnız OKUMA yapabilir; veri değiştiren ya da izinli vitrin yüzeyi dışına çıkan
  her istek çalıştırılmadan reddedilir; sonuç boyutu ve çalışma süresi üst-sınırlıdır.
- **FR-005**: İzinli vitrin yüzeyi YALNIZ satılabilir ürünleri ve onların sunuma açık alanlarını
  içerir; kullanıcı satın-alma kayıtları ve diğer her veri yüzeyin DIŞINDADIR (yapısal olarak).
- **FR-006**: Kurulan her sorgu ve sonucu (en az: sorgu metni, başarı/ret, sonuç sayısı) kayıt
  altına alınır.
- **FR-007**: Reddedilen sorguda asistana makine-okur hata verilir; asistan sınırlı deneme hakkıyla
  düzeltir; kalıcı başarısızlıkta kullanıcıya dürüst geri bildirim verilir.
- **FR-008**: Yanıt kalitesi temsilî bir soru setiyle (eval) doğrulanabilir olmalıdır: eski yüzeyin
  karşıladığı TÜM soru sınıfları + yeni açılan sınıflar için beklenen-sonuç desenleri tanımlanır.
- **FR-009**: Catalog'un envanter listeleri (kategori/yazar/yayınevi) ve Storefront'un iç okuma
  yüzeyleri (kişisel feed vb.) bu değişiklikten ETKİLENMEZ.

### Key Entities

- **Sorgu isteği**: Asistanın kurduğu okuma-sorgusu + (varsa) anlamsal metin parçaları + sayfa bilgisi.
- **İzinli vitrin yüzeyi**: Satılabilir ürünlerin sunuma açık alanları (ad, açıklama, yazarlar,
  yayınevi, kategori, fiyat, stok, puan, özellikler, aile kodu, kapak, yaklaşık ekleniş) + anlamsal
  temsil. Tek tutarlı ilişki olarak görünür.
- **Sorgu izi**: Sorgu metni, karar (çalıştı/reddedildi), sonuç sayısı, süre.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Eval setindeki soru sınıflarının (eski yüzeyin tümü + istatistik/karşılaştırma/VEYA/
  filtreli-benzerlik) ≥ %90'ı ilk ya da düzeltme denemesinde doğru yanıtlanır; hiçbir sınıf
  "yanıtlanamaz" kalmaz (bilinen iki veri sınırı hariç: satış adedi, kesin ekleniş tarihi).
- **SC-002**: 067'nin canlı geçen senaryoları (temalı arama kısıt uyumu, benzerlikte kendisi-hariç,
  alakasızda "bulunamadı") yeni kapıda da geçer — anlamsal yetenekte sıfır gerileme.
- **SC-003**: Güvenlik denemelerinin (veri değiştirme, izinsiz yüzey, tavan aşımı) %100'ü
  çalıştırılmadan reddedilir; kullanıcı satın-alma kayıtlarına tek bir sorgu bile ulaşamaz.
- **SC-004**: Yayından kaldırılan ürün hiçbir sorgu yanıtında görünmez (yapısal garanti).
- **SC-005**: Her sorgu izlenebilir: rastgele seçilen herhangi bir chat yanıtının arkasındaki sorgu
  metni ve sonuç sayısı kayıtlardan bulunabilir.

## Assumptions

- Kilitli tasarım kararları (brainstorm + spike, 2026-09-07): asistan sorguyu kendisi kurar; tek
  güvenlik zemini "izinli vitrin yüzeyi" (tek tutarlı ilişki) + çalıştırma-öncesi bekçi denetimi;
  anlamsal metin yer-tutucuyla iletilir, dönüşümü sistem yapar. Ayrıntı proje memory'sindedir
  (`069-query-storefront-direction`).
- Fiziksel "tek tablo" yolu spike ile elendi (araç kısıtları); "tek ilişki" deneyimi mantıksal
  katmanla sağlanır — kullanıcıya/asistana görünen dünya tek yüzeydir.
- İtiraz kaydı (bilinçli kabul): deterministik parametre garantilerinden vazgeçilip kalite
  eval + iz kayıtlarıyla yönetilecektir; gecikme (sorgu kurma turu) kabul edilir.
- Bilinçli kabul (kaynak koruması): "kilitleyemez" güvencesi TEK-SORGU bazındadır (timeout +
  satır tavanı); anonim yüzeyden tekrarlı-sorgu seline (flood) oran sınırı BU feature'da yok —
  iz kayıtları izler; gerekirse ayrı hardening feature'ı.
- Kapsam dışı: satış sayacı/bestseller (veri yok — ayrı feature), kesin ekleniş tarihi (kaynak
  event genişletmesi ister), Catalog envanter listeleri, diğer BC yüzeyleri, iç okuma yüzeyleri.
- 068 spec'i İPTAL (arşiv notu düşüldü); id-bazlı filtre ve sayfalama fikirleri bu kapının sorgu
  kalıplarında yaşar.