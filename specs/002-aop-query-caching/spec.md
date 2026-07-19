# Feature Specification: AOP Query Caching (Two-Tier Declarative Read Caching)

**Feature Branch**: `002-aop-query-caching`

**Created**: 2026-07-19

**Status**: Draft

**Input**: User description: "Projemde caching mekanizması eksik. Kompleks sorgular için
Redis içime sinmiyor. Caching handler içine kod yazmadan, her handler'a tek tek sarmadan —
AOP standardında, declarative bir cross-cutting concern olsun. İki katman: önce MemoryCache,
yoksa Redis, orada da yoksa kaynağa sorgu. İlk etapta Catalog okumalarıyla başlayalım."

## Artefakt Ölçekleme Kademesi

**Tam (Full)** — öneri: bu feature `/speckit-plan` üretmelidir. Gerekçe: yeni bir
cross-cutting altyapı mekanizması (declarative iki-katmanlı caching aspect) ve paylaşımlı
(distributed) bir katman getiriyor; tazelik/invalidation ve cross-instance tutarlılık karar
gerektiriyor. Anayasa: "Şüphedeyse bir üst kademe."

## Clarifications

### Session 2026-07-19

- Q: Catalog önbellek anahtarı kullanıcı/scope bağlamı içermeli mi? → A: İçermez —
  paylaşımlı (kullanıcı-bağımsız) anahtar; bir ürün = tüm kullanıcılar için tek girdi.
  Yetki cache'ten önce endpoint'te CatalogRead ile zorlanır; veri kullanıcıya özel değil.
- Q: Cross-instance L1 tazeliği v1'de nasıl sağlanır? → A: Kısa TTL ile sınırlanır —
  L1 TTL ≤ 5sn; başka instance'ın bayat L1'i kendiliğinden dolar. v1'de backplane yok
  (L2 zaten tüm instance'larda tutarlı). Backplane ilerideki bir iş olarak ertelenir.
- Q: Bir ürün değişince geçersizleştirme taneliği ne olsun? → A: Kaba — tek
  `catalog-products` etiketi; her yazma tüm katalog girdilerini (by-id + listeler) boşaltır.
  Her zaman doğru; per-ürün granular etiketleme ileriye ertelendi (yazmalar seyrek).
- Q: v1'de önbellek gözlemlenebilirliği kapsamda mı? → A: Evet, minimal — L1/L2 hit,
  miss ve eviction sayaçları (+opsiyonel log) tek middleware'den yayılır. Tam
  dağıtık tracing ertelenir. SC-001/SC-002'nin pratikte doğrulanmasını sağlar.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tekrarlı katalog okumaları iki katmandan hızlı yanıtlanır (Priority: P1)

Bir istemci (web/agent) aynı katalog verisini kısa aralıklarla tekrar okur; okuma önce
hızlı yerel katmana (L1), orada yoksa paylaşımlı katmana (L2) bakar, ikisinde de yoksa
kaynağa sorgu atar ve sonucu iki katmana da doldurur.

**Why this priority**: Feature'ın çekirdek değeri budur — tekrarlı okumalarda gecikme ve
veri kaynağı yükü düşer. Tek başına teslim edilse bile ölçülebilir kazanç sağlar.

**Independent Test**: Aynı katalog listesi/ürünü peş peşe istenir; ilk istek kaynağı
sorgular ve iki katmanı doldurur, sonraki istekler kaynağa gitmeden yanıtlanır.

**Acceptance Scenarios**:

1. **Given** veri hiçbir katmanda yokken, **When** ürün listesi ilk kez istenir, **Then**
   kaynaktan hesaplanır ve hem L1 hem L2'ye yazılır.
2. **Given** veri L1'de varken, **When** aynı sorgu tekrar istenir, **Then** sonuç L2 ve
   kaynağa gidilmeden L1'den döner; içerik birebir aynıdır.
3. **Given** veri L1'de yok ama L2'de varken (ör. L1 süresi dolmuş), **When** sorgu
   istenir, **Then** sonuç kaynağa gidilmeden L2'den döner ve L1'e geri yazılır.
4. **Given** aynı sorgu farklı parametreyle istenmişken, **When** çağrı yapılır, **Then**
   her parametre kombinasyonu ayrı bir önbellek girdisi olur.

---

### User Story 2 - Yazma sonrası okumalar güncel veriyi yansıtır (Priority: P2)

Bir ürün oluşturulunca, güncellenince veya silinince, o veriye ait sonraki okumalar bayat
değeri değil güncel değeri döner; geçersizleştirme her iki katmanı da temizler.

**Why this priority**: Doğruluk için gerekli; ama P1 için kısa TTL bir emniyet ağı
sağladığından, yazma-tetikli invalidation ayrı ve ikincil bir dilim olarak eklenir.

**Independent Test**: Bir ürün iki katmana alınacak şekilde okunur; ardından değiştirilir;
sonraki okuma TTL süresini beklemeden yeni değeri döner.

**Acceptance Scenarios**:

1. **Given** bir ürün okunup iki katmana alınmışken, **When** aynı ürün güncellenir,
   **Then** sonraki okuma güncel değeri döner (hem L1 hem L2 tazelenir).
2. **Given** ürün listesi önbellekteyken, **When** yeni bir ürün eklenir, **Then** sonraki
   liste okuması yeni ürünü içerir.
3. **Given** bir ürün önbellekteyken, **When** ürün silinir (soft-delete), **Then**
   sonraki okuma silinmiş ürünü döndürmez.

---

### User Story 3 - Önbellek bir sorguya kod yazmadan eklenir (Priority: P2)

Bir geliştirici, mevcut bir okuma sorgusunu önbelleklenebilir yapmak için sorgunun iş
mantığına dokunmaz; önbelleğe alma tek bir bildirimsel işaretle etkinleşir. Yazma tarafında
geçersizleştirme de bildirimsel bir işaretle sağlanır.

**Why this priority**: Kullanıcının pazarlık edilemez kısıtı — caching bir cross-cutting
aspect'tir. Bu olmadan feature "doğru" sayılmaz; mekanizmanın genişlemesi buna bağlıdır.

**Independent Test**: Bir okuma sorgusu önbellekli hale getirilir; sorgu ve komut
handler'larının iş-mantığı gövdesinde hiçbir önbellek çağrısı bulunmadığı incelemeyle
doğrulanır.

**Acceptance Scenarios**:

1. **Given** önbelleksiz bir okuma sorgusu, **When** bildirimsel işaret eklenir, **Then**
   sorgu davranışı değişmeden iki katmanda önbelleklenir ve handler kodu caching içermez.
2. **Given** bir yazma işlemi, **When** bildirimsel geçersizleştirme işareti eklenir,
   **Then** başarılı yazmadan sonra ilgili girdiler iki katmandan handler kodu olmadan boşalır.
3. **Given** önbellekli bir sorgu, **When** işaret kaldırılır, **Then** sorgu doğrudan
   kaynaktan yanıtlanır ve başka kod değişikliği gerekmez.

---

### Edge Cases

- Önbelleğe alınan sorgu **bulunamadı (NotFound)** sonucu döndürürse — negatif sonuç
  önbelleklenmez, her istekte yeniden değerlendirilir (bkz. Assumptions).
- Aynı girdi **eşzamanlı çok istek** ile ilk kez ısıtılırken tek hesaplama yapılmalı
  (cache-stampede önlenmeli), N kez kaynağa gidilmemeli.
- **L2 (paylaşımlı katman) erişilemezse** okuma L1'den ya da kaynaktan doğru yanıtlanmalı;
  caching bir tek-nokta-arıza kaynağı olmamalı.
- **Her iki katman da erişilemezse** okuma yine de kaynaktan doğru yanıtlanmalı.
- **Yazma/okuma yarışı**: geçersizleştirme yazma **commit'inden sonra** yapılmalı; aksi
  halde eşzamanlı bir okuma bayat değeri yeniden yazabilir. Kısa TTL bunu sınırlar.
- **Cross-instance**: bir instance yazınca diğer instance'ların L1'i bayat kalabilir;
  paylaşımlı L2 tutarlıdır, L1 tazeliği backplane veya kısa TTL ile sınırlanır.
- **Yetki/kullanıcı bağlamı**: kullanıcıya özel bir sonuç başka kullanıcının yetkisiyle
  sızmamalı; kullanıcıya-özel okumalar bağlamıyla anahtarlanmalı.
- İşaretli sorgu **parametre taşımıyorsa** (parametresiz liste) tutarlı bir anahtar üretilmeli.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem işaretlenmiş okuma sorgularının sonuçlarını MUST önbelleğe alsın; aynı
  sorgu+parametre tekrarında sonucu kaynağa gitmeden döndürsün.
- **FR-002**: Okuma MUST iki katmanlı sırayı izlesin: önce yerel hızlı katman (L1), yoksa
  paylaşımlı katman (L2), ikisinde de yoksa kaynak; kaynaktan gelen sonuç iki katmana yazılsın.
- **FR-003**: L2'de bulunup L1'de bulunmayan bir sonuç MUST L1'e geri yazılsın (repopulation).
- **FR-004**: Önbellek girdileri MUST sorgu kimliği + tüm ayırt edici girdi parametreleri
  ile benzersiz anahtarlansın. Catalog kapsamında anahtar **paylaşımlıdır** (kullanıcı/yetki
  bağlamı içermez); sonuç kullanıcıya özel olan gelecekteki kapsamlar bağlamla anahtarlanabilir.
- **FR-005**: Sistem her iki katman için MUST bir yaşam süresi (TTL) uygulasın; süresi dolan
  girdi bir sonraki okumada yeniden hesaplanıp tazelensin. Cross-instance bayatlığı sınırlamak
  için L1 TTL MUST ≤ 5sn olsun (SC-004 penceresi); L2 TTL daha uzun bir backstop olabilir.
- **FR-006**: İlgili veri değiştiğinde sistem, o veriye ait girdileri mantıksal bir etiketle
  MUST **her iki katmandan** geçersiz kılsın; geçersizleştirme yazma **commit'inden sonra** olsun.
  v1'de etiket **kaba tanelidir**: tek bir `catalog-products` etiketi tüm katalog girdilerini
  (by-id + listeler) topluca boşaltır. Herhangi bir Catalog yazması bu etiketi geçersiz kılar.
- **FR-007**: Önbelleğe alma ve geçersizleştirme MUST bildirimsel/cross-cutting olsun; ne
  sorgu ne komut handler'ının iş-mantığı gövdesine önbellek kodu yazılmasın.
- **FR-008**: Aynı işaret ile bir sorgu önbelleklenebilir; işaret kaldırılınca MUST
  önbelleksiz çalışsın — başka kod değişikliği gerekmeden.
- **FR-009**: Bir girdi ilk kez ısıtılırken sistem MUST eşzamanlı istekleri tek hesaplamada
  birleştirsin (cache-stampede olmasın).
- **FR-010**: Herhangi bir önbellek katmanı erişilemezse okuma MUST kaynaktan (veya erişilebilen
  katmandan) doğru yanıtlansın; caching kullanılabilirlik tek-nokta-arıza kaynağı olmasın.
- **FR-011**: Önbelleğe alma MUST yalnızca okuma (query) tarafına uygulansın; durumu
  değiştiren işlemler önbelleklenmesin (CQRS ayrımı korunur).
- **FR-012**: İlk teslimat kapsamı MUST yalnızca Catalog okumaları olsun (ürün listesi ve
  ürün-by-id); diğer servisler aynı desenle sonradan eklenebilir.
- **FR-013**: Önbelleklenmiş bir yanıt, aynı durum için önbelleksiz yanıtla MUST birebir aynı
  olsun (biçim/içerik farkı olmasın).
- **FR-014**: Sistem MUST minimal önbellek gözlemlenebilirliği yaysın: L1/L2 hit, miss ve
  eviction sayaçları (isteğe bağlı log). Bu sinyaller cross-cutting katmandan üretilir; handler
  gövdesine kod eklenmez. Tam dağıtık tracing v1 dışıdır (ertelenir).

### Key Entities *(include if feature involves data)*

- **Önbellek Girdisi (Cache Entry)**: Bir okuma sorgusunun belirli girdilerle üretilmiş
  sonucunun geçici, anahtarlı kopyası; yerel (L1) ve paylaşımlı (L2) katmanda yaşayabilir.
  Nitelikler: anahtar, saklanan sonuç, yaşam süresi (katman başına olabilir).
- **Geçersizleştirme Etiketi (Invalidation Tag)**: Bir veri kümesine ait girdileri iki katmanda
  topluca geçersiz kılan mantıksal etiket. v1'de kaba tanelidir: tüm katalog için tek
  `catalog-products` etiketi. Girdinin hangi sorgudan doğduğu **saklanmaz**; yalnızca kaynağı
  temsil eden etiket saklanır.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Yerel katmandan (L1) yanıtlanan tekrarlı okumalar, ilk (soğuk) okumaya kıyasla
  en az %80 daha hızlı döner.
- **SC-002**: Isınmış önbellekle tekrarlı okumalar boyunca kaynağa giden sorgu sayısı,
  önbelleksiz duruma göre en az %90 azalır.
- **SC-003**: L1'de olmayıp L2'de bulunan bir sonuç, kaynağa hiç gidilmeden döner (kaynak
  sorgu sayısı 0).
- **SC-004**: Bir veri değişikliğinin ardından okumalar güncel değeri en geç 5 saniye içinde
  yansıtır (her iki katman temizlenir).
- **SC-005**: Bir okuma sorgusuna önbelleğe alma eklemek, o sorgunun iş-mantığı gövdesinde 0
  satır önbellek kodu gerektirir (inceleme ile doğrulanır).
- **SC-006**: Aynı girdi için eşzamanlı 100 ilk-istek altında kaynak en fazla 1 kez sorgulanır.
- **SC-007**: Paylaşımlı katman (L2) devre dışıyken bile katalog okumaları %100 doğru sonuç
  döndürmeye devam eder (hata oranında artış olmaz).
- **SC-008**: Önbellek hit/miss/eviction sayaçları gözlemlenebilir; ısınmış önbellekte
  ölçülen hit oranı SC-002'yi (kaynağa giden sorguda ≥%90 azalma) doğrulayacak şekilde raporlanabilir.

## Assumptions

- **Yaklaşım**: Önbelleğe alma, uygulama-içi bildirimsel bir cross-cutting aspect ile
  sağlanır (AOP); okuma iki katmanlı L1→L2→kaynak sırasını izler.
- **Somut teknoloji (kullanıcı kararı)**: L1 = süreç-içi bellek önbelleği (MemoryCache), L2 =
  paylaşımlı Redis. Kesin bağlama/kütüphane seçimi plan aşamasında netleşir.
- **Tutarlılık modeli**: Yazma, commit sonrası iki katmanı da geçersiz kılar; kısa TTL emniyet
  ağıdır. Kısa süreli bayatlık katalog verisi için kabul edilebilir.
- **Cross-instance**: Paylaşımlı L2 tüm instance'larda tutarlıdır; per-instance L1 tazeliği
  v1'de **kısa TTL** (≤ 5sn) ile sınırlanır — dağıtık backplane v1 kapsamı dışıdır, ileriye
  ertelendi. Böylece başka instance'ın bayat L1 kopyası SC-004 penceresinde kendiliğinden dolar.
- **Kapsam sınırı**: İlk sürüm yalnızca Catalog okumalarını kapsar; kompleks/çok-kaynaklı
  sorgular önbelleklenmez — onlar için okuma-modeli/projeksiyon ayrı ve ertelenmiş bir iştir.
- **Sorgu izi (provenance) tutulmaz**: Girdide/read model'de verinin hangi sorgudan doğduğu
  saklanmaz; bilinçli erteleme, Obsidian'da `todo-readmodel-query-provenance` olarak not edildi.
- **Negatif sonuç**: NotFound sonuçları önbelleklenmez; her istekte yeniden değerlendirilir.
- **Bağımlılık**: Mevcut okuma sorguları ve mesaj-bus'ı yeniden kullanılır; yeni tablo veya yeni
  endpoint kontratı gerekmez. Paylaşımlı önbellek altyapısı (Redis) bir çalışma-anı bağımlılığıdır.