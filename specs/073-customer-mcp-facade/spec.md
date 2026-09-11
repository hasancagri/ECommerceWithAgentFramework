# Feature Specification: Tek Müşteri MCP Fasadı

**Feature Branch**: `073-customer-mcp-facade`

**Created**: 2026-09-11

**Status**: Draft

**Input**: User description: "Müşteri kendi AI istemcisinden mağazaya her BC için ayrı ayrı bağlanmak + ayrı ayrı login olmak yerine TEK bir MCP ucuna bağlanır ve TEK kez giriş yapar; tüm müşteri tool'ları o tek uçtan görünür."

**Artefakt kademesi**: **Tam** — yeni izole servis (fasad), yeni dış erişim yüzeyi, yeni kimlik istemcisi
+ tek-consent akışı, servisler-arası yeni yönlendirme deseni. Tam akış.

## Bağlam (neden bu feature)

Bugün müşterinin agent'ı (kendi AI istemcisi, ör. Claude Desktop) mağazayla konuşmak için her Bounded
Context'in MCP ucuna **ayrı ayrı** bağlanmak ve korumalı her uç için **ayrı ayrı giriş (consent)**
yapmak zorunda. Bu, altı-yedi kez tekrarlanan bir login yorgunluğu ve dağınık bir kurulum demek.

Bu feature, müşteri için **tek bir MCP kapısı** açar: müşteri tek uca bağlanır ve tüm müşteri
tool'larını (ürün arama, sepet, sipariş, ödeme bağlamı) o tek uçtan görür. Fasad, gelen çağrıyı doğru
BC'ye yönlendirir; müşterinin kimliğini (yetkisini) o BC'ye taşır.

**Login e-ticaret normuna uyar — anonim gez, satın alırken gir.** Kapıya bağlanmak ve gezinmek giriş
İSTEMEZ: ürün arama/keşif ve **anonim sepet** (cihaza bağlı geçici kimlik) girişsiz çalışır. Giriş
**yalnız satın almada** (sipariş/ödeme) ve **bir kez** (step-up) istenir; giriş anında anonim sepet
kullanıcıya devredilir. Yani "tek kapı + tek giriş" korunur ama giriş öne dayatılmaz, satın almaya ertelenir.

Bu, mağazanın kendi sohbet agent'ının (ChatAgent) kaldırılıp müşteri akıl-yürütmesinin kullanıcının
kendi istemcisine taşındığı yöne (BYO-agent) hizmet eder. **ChatAgent söküm bu feature'ın parçası
DEĞİL** — bu feature yalnız fasadı kurar; fasad canlı doğrulandıktan sonra söküm ayrı bir iştir.

Fasad, dış ticaret platformları için REST kapısı olan **UCP'yi değiştirmez, tamamlar**: UCP dış
platform personası (ChatGPT/Gemini arka-ucu), bu fasad ise **bireysel müşteri** personasıdır (kendi
MCP istemcisi). İki ayrı transport, iki ayrı persona.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Anonim gez + sepete ekle, satın alırken tek kez giriş yap (Priority: P1) 🎯 MVP

Müşteri kendi AI istemcisine **tek bir mağaza MCP'si** ekler. **Giriş yapmadan** ürün arar, keşfeder ve
**anonim sepetine** ürün ekler. Satın almaya karar verince (checkout) **bir kez** giriş yapar; giriş
anında anonim sepeti kullanıcı hesabına devredilir. Giriş sonrası checkout fazında adresini seçer/ekler,
kargosunu ve ödemesini yaparak siparişi tamamlar. Tüm bu akış **tek uçtan** yürür; farklı BC'lere ait
tool'lar tek listede görünür, çağrılar arka planda doğru BC'ye gider.

**Why this priority**: Feature'ın var oluş nedeni ve çekirdek değeri; gerçek e-ticaret akışını (anonim
gez → satın alırken gir) tek kapı + tek girişle sağlar → MVP.

**Independent Test**: İstemciye yalnız fasad MCP'si eklenir; **girişsiz** ürün aranıp anonim sepete
eklenir; checkout başlatılınca **tek** giriş istenir; sepet kullanıcıya taşınır; adres seçilip sipariş
tamamlanır — süreç boyunca ikinci bir giriş sorulmadan.

**Acceptance Scenarios**:

1. **Given** müşteri fasada yeni bağlandı (girişsiz), **When** ürün arar veya anonim sepete ekler,
   **Then** işlem giriş istenmeden çalışır.
2. **Given** girişsiz dolu bir anonim sepet, **When** müşteri checkout'u başlatır, **Then** yalnız BİR
   kez giriş/consent istenir ve anonim sepet kullanıcı hesabına devredilir (kalemler korunur).
3. **Given** giriş yapılmış checkout fazı, **When** müşteri adresini seçer/ekler ve siparişi tamamlar,
   **Then** sipariş oluşur ve akış boyunca ikinci giriş istenmez.
4. **Given** giriş yapılmış oturum, **When** tool listesi istenir, **Then** birden çok BC'nin müşteri
   tool'ları tek birleşik listede döner ve çağrılar doğru BC'de müşterinin yetkisiyle çalışır.
5. **Given** yetkisi olmayan bir işlem, **When** çağrı yapılır, **Then** ilgili BC yetki reddini döner
   ve fasad bu reddi müşteriye iletir (fasad çökmez, oturum düşmez).

---

### User Story 2 - Yönetici ayrı bir tek-kapıdan, tek girişle yönetim yapar (Priority: P2)

Yönetici, müşteri kapısından **ayrı** bir yönetim MCP kapısına bağlanır, bir kez (yönetim yetkisiyle)
giriş yapar ve yönetim tool'larına (ör. katalog/stok/merchant yönetimi) tek uçtan erişir.

**Why this priority**: Yönetim akışı ayrık bir güvenlik yüzeyidir; müşteri MVP'sini bloke etmez ama
aynı tek-kapı ergonomisini yönetim için de sağlar → P2.

**Independent Test**: Yönetim ucuna bağlanılır; yönetim yetkisiyle tek giriş sonrası yalnız yönetim
tool'ları görünür (müşteri tool'ları görünmez), müşteri ucunda ise yönetim tool'ları görünmez.

**Acceptance Scenarios**:

1. **Given** yönetici yönetim kapısına bağlanıyor, **When** giriş yapılır, **Then** yalnız yönetim
   tool'ları listelenir ve müşteri tool'ları listelenmez.
2. **Given** müşteri kapısı, **When** tool listesi istenir, **Then** yönetim tool'ları GÖRÜNMEZ (yüzey ayrımı).

---

### User Story 3 - Bir servis çevrimdışıyken kapı ayakta kalır (Priority: P2)

Alt servislerden biri geçici olarak erişilemez olduğunda, müşteri diğer servislerin tool'larını
kullanmaya devam edebilmeli; kapı komple çökmemeli ve servis geri gelince tool'ları yeniden
görünmeli — yeniden bağlanma/restart gerekmeden.

**Why this priority**: Dağıtık sistemde kısmi kesinti kaçınılmaz; dayanıklılık çekirdek deneyimi korur
ama MVP satın almayı bloke etmez → P2.

**Independent Test**: Bir BC durdurulur; fasadda tool listesi istenir → o BC'nin tool'ları eksik ama
diğerleri listelenir ve kullanılabilir; BC geri başlatılıp tekrar liste istenince tool'ları geri gelir
(kalıcı kayıp yok, restart yok).

**Acceptance Scenarios**:

1. **Given** bir BC çevrimdışı, **When** tool listesi istenir, **Then** diğer BC'lerin tool'ları döner
   ve fasad hata vermez.
2. **Given** BC yeniden çevrimiçi, **When** tool listesi yeniden istenir, **Then** o BC'nin tool'ları
   yeniden görünür (müşteri yeniden bağlanmadan).

---

### Edge Cases

- Aynı ada sahip tool birden çok BC'de bulunursa: tool adları mağaza genelinde benzersiz olduğundan
  çakışma beklenmez; yine de belirsizlik oluşursa deterministik tek sahip seçilir ve durum loglanır.
- Fasadın tanımadığı (kayıtta olmayan) bir tool çağrılırsa: açıklayıcı hata döner, çağrı sessizce yutulmaz.
- Müşteri girişsiz gezinip anonim sepete ekler; **checkout başlatınca** giriş akışı tetiklenir (tek
  kapıya tek giriş, satın alma anında).
- Giriş anında anonim sepet boşsa: devredilecek kalem yoktur, akış normal ilerler.
- Giriş sonrası anonim sepet kullanıcının mevcut sepetiyle çakışırsa: kalemler birleştirilir (kayıp yok).
- Yeni kart girişi istenirse: fasad kart alanı TOPLAMAZ; ödeme sağlayıcısının barındırdığı forma
  yönlendiren bir bağlantı/işaret döner.
- Alt servis, müşterinin yetkisini reddederse (eksik izin): fasad reddi iletir; diğer tool'lar etkilenmez.
- Yönetim kapısına müşteri yetkisiyle erişim: yönetim tool'ları verilmez (yüzey + yetki ayrımı).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, müşteri için **tek bir MCP kapısı** sunmalı; müşteri kendi istemcisinde yalnız bu
  tek ucu tanımlayarak tüm müşteri tool'larına erişebilmeli.
- **FR-002**: Kapı **girişsiz bağlanabilmeli**: bağlanma, tool listeleme, ürün arama/keşif ve anonim
  sepet işlemleri giriş İSTEMEMELİ. Giriş **yalnız satın alma (checkout) başlatıldığında** ve **tek kez**
  (step-up) istenmeli; tek giriş sonrası hiçbir alt işlem için tekrar giriş istenmemeli (alt servislerin
  kimlik itirazları müşteriye ulaşmamalı).
- **FR-002a**: Kapı, **anonim sepeti** desteklemeli (giriş öncesi cihaza/oturuma bağlı geçici kimlikle);
  müşteri giriş yaptığında anonim sepet kullanıcı hesabına **devredilmeli** (kalemler kaybolmadan).
- **FR-002b**: Checkout fazında (giriş sonrası) müşteri **teslimat adresini seçebilmeli veya
  ekleyebilmeli** (adres hesaba bağlıdır; giriş gerektirir).
- **FR-002c**: Ödeme aracı: müşteri **kayıtlı (tokenize) kartını seçebilmeli**; **yeni kart girişi
  mağazada/fasadda YAPILMAZ** — ödeme sağlayıcısının barındırdığı forma yönlendirilir. Kart PAN'ı
  hiçbir koşulda fasaddan/agent'tan/LLM'den geçmemeli (yalnız tokenize referans).
- **FR-003**: Kapı, birden çok Bounded Context'in müşteri tool'larını **tek birleşik tool listesi**
  olarak sunmalı.
- **FR-004**: Kapı, bir tool çağrısını **doğru sahip BC'ye yönlendirmeli** ve müşterinin kimliğini/yetkisini
  o BC'ye taşımalı; sahip BC işi kendi yetki denetimiyle yürütmeli.
- **FR-005**: Kapı, kendi başına iş mantığı yürütmemeli — yalnız keşif + yönlendirme (proxy) yapmalı;
  her BC kendi verisinin ve kurallarının tek sahibi kalmalı.
- **FR-006**: Kapı, tool listesini **isteğe bağlı/oturum anında** (önceden sabitlenmiş anlık görüntü
  DEĞİL) toplamalı; bir BC geç açılır ya da geçici erişilemezse **kalıcı tool kaybı olmamalı** — servis
  geri geldiğinde tool'ları sonraki listelemede yeniden görünmeli (yeniden bağlanma gerekmeden).
- **FR-007**: Bir BC erişilemezken kapı **çökmemeli**; erişilemez BC'nin tool'ları o an listelenmez,
  diğer BC'lerin tool'ları kullanılabilir kalmalı (graceful degrade).
- **FR-008**: Sistem, **müşteri kapısı ile yönetim kapısını ayırmalı**: her kapı kendi tek-giriş
  akışına ve kendi tool kümesine sahip olmalı; müşteri kapısında yönetim tool'ları görünmemeli ve tersi.
- **FR-009**: Fasad, mevcut BC MCP uçlarıyla **birlikte yaşamalı** (additive); mevcut iç tüketiciler
  (ör. ChatAgent keşfi) etkilenmemeli.
- **FR-010**: Fasad, müşteri kimlik bilgisini (yetki jetonunu) **kalıcı saklamamalı**; her çağrıda o
  anki müşteri yetkisini taşımalı.
- **FR-011**: Dış ticaret platformu kanalı (UCP, REST) bu feature'dan **etkilenmemeli**; fasad ayrı bir
  persona/transport'tur ve UCP'yi değiştirmez.

### Key Entities *(include if feature involves data)*

- **Müşteri MCP Kapısı**: Müşterinin kendi istemcisinden bağlandığı tek dış uç; tek giriş + birleşik
  tool listesi + yönlendirme sağlar.
- **Yönetim MCP Kapısı**: Müşteri kapısından ayrı, yönetim yetkisiyle tek girişli yönetim yüzeyi.
- **Tool Yönlendirme Kaydı**: Tool adı → sahip Bounded Context eşlemesi ve yüzey (müşteri/yönetim) bilgisi.
- **Birleşik Tool Kataloğu**: Alt BC'lerden oturum anında toplanan, yüzeye göre süzülmüş tool listesi.
- **Müşteri Kimlik İstemcisi**: Tek-giriş/consent'i mümkün kılan, müşteri yetki demetini talep eden
  kayıtlı dış-agent kimliği (yönetim için ayrı kimlik).
- **Anonim Sepet Kimliği**: Giriş öncesi sepeti taşıyan geçici (cihaza/oturuma bağlı) kimlik; giriş
  anında kullanıcı hesabına devredilir.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Müşteri, kendi istemcisine **tek MCP kapısı** ekleyerek tüm müşteri tool'larına erişir —
  BC sayısından bağımsız olarak istemcide tanımlanan mağaza MCP sayısı **tam olarak 1**.
- **SC-002**: Gezme + arama + anonim sepet **girişsiz** tamamlanır (bu aşamada giriş sayısı **0**); bir
  müşteri satın alma yolculuğunda istenen toplam giriş/consent sayısı **tam olarak 1** (checkout anında).
- **SC-003**: Anonim sepete eklenen kalemler, giriş sonrası kullanıcı sepetinde **%100 korunur** (devir
  sırasında kalem kaybı 0).
- **SC-003b**: Tek girişten sonra müşteri; adres seçme/ekleme, kayıtlı kart seçme ve sipariş vermeyi ek
  kuruluma gerek kalmadan tamamlayabilir; kart PAN'ı hiçbir aşamada fasaddan geçmez.
- **SC-004**: Tool çağrıları **doğru BC'ye** yönlenir (ör. sepet işlemi sepet servisinde, sipariş
  işlemi sipariş servisinde gerçekleşir) — yanlış-yönlendirme **%0**.
- **SC-005**: Bir BC çevrimdışıyken diğer BC'lerin tool'ları çalışmaya devam eder; BC geri geldiğinde
  tool'ları **yeniden bağlanma olmadan** yeniden görünür (kalıcı tool kaybı **%0**).
- **SC-006**: Yönetim tool'ları müşteri kapısında **hiçbir koşulda** görünmez; müşteri tool'ları
  yönetim kapısında görünmez (yüzey sızıntısı **%0**).

## Assumptions

- Müşteri, kendi AI istemcisinde uzak MCP ucu tanımlayabilir ve standart OAuth giriş akışını yürütebilir.
- Mağaza tool adları BC'ler arasında benzersizdir; bu yüzden birleşik listede ad-çakışması olmaz.
- Tek giriş, müşteri işlemleri için gereken yetki demetini kapsayan bir kimlikle yürütülür (yönetim
  için ayrı, yönetim yetki demetli kimlik).
- Gezme/arama + anonim sepet giriş gerektirmez; giriş satın almada (checkout) bir kez tetiklenir.
- Anonim sepet, giriş öncesi cihaza/oturuma bağlı geçici bir kimlikle tutulur; giriş anında kullanıcıya
  devredilir (mağazanın mevcut anonim-sepet + login-devir yeteneği bu feature'da agent yoluna açılır).
- Kart girişi mağazanın/fasadın işi DEĞİL: yeni kart ödeme sağlayıcısının barındırdığı formda alınır;
  mağaza yalnız tokenize kartı seçer. PAN fasaddan/agent'tan/LLM'den geçmez.
- Alt BC MCP uçları ve iç tüketiciler (ChatAgent keşfi) yerinde kalır; bu feature onları değiştirmez.
- ChatAgent'ın söküm işi bu feature'ın kapsamı dışındadır (ayrı feature, fasad canlı PASS sonrası).

## Dependencies

- **Identity sunucusu**: müşteri/yönetim tek-giriş akışı için kayıtlı dış-agent kimliği + tek consent.
- **Müşteri Bounded Context'leri** (sepet/sipariş/müşteri/ödeme/vitrin/katalog): tool'ları toplanan ve
  çağrıları yönlendirilen sahip servisler; MCP uçları yerinde kalır.
- **Tek giriş kapısı (gateway)**: fasadın tek dış giriş noktasından yayınlanması.
- **Anonim sepet + login-devir**: giriş öncesi anonim sepet ve giriş anında kullanıcıya devir yeteneği
  (mağazada mevcut; bu feature agent/fasad yoluna açar — bilinen boşluk giderilir).
- **Ödeme sağlayıcısı (barındırılan kart formu)**: yeni kart girişi mağaza dışı bu formda alınır; fasad
  yalnız yönlendirme bağlantısı sunar. Bu feature ödeme sağlayıcısını değiştirmez.