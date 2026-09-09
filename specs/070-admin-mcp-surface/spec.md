# Feature Specification: Admin Yüzeyinin MCP'ye Taşınması (Agent-Only Yönetim)

**Feature Branch**: `070-admin-mcp-surface`

**Created**: 2026-09-08

**Status**: Draft

**Input**: User description: "Admin yüzeyini MCP'ye taşı (agent-only yönetim): catalog admin tool'ları,
stock admin, merchant kimlik, quote_installments, seed'li admin OAuth istemcisi, 069 playbook göçü,
SignUp kontrolü. Yazma tool'ları dar + audit izi. Tüketici: Claude Desktop. WebApp/ChatAgent sökümü
kapsam DIŞI (ayrı feature)."

## Vizyon bağlamı

Mağaza "ekran satan" değil "kontrat sunan" modele geçiyor: veri + aksiyon MCP tool'larıyla sunulur,
arayüzü kullanıcının kendi agent'ı (Claude Desktop) anlık üretir (generative UI). Bu spec, son ekran
bağımlılığı olan ADMİN işlemlerini bu modele taşır. Başarıyla bitince WebApp + ChatAgent sökümü
(ayrı feature) önünde engel kalmaz.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin Claude Desktop'tan ürün yönetir (Priority: P1)

Mağaza yöneticisi, Claude Desktop'ına mağazanın MCP bağlantısını ekler; yönetim yetkili hesabıyla
OAuth girişi yapar. Sohbetle ürünleri listeler/arar, birinin detayına iner, künyesini (ad, açıklama,
fiyat, yazar/yayınevi/kategori bağları) günceller, yayına alır/yayından kaldırır, fiyat geçmişine
bakar. Agent, dönen veriden tabloyu/görünümü kendi kurar.

**Why this priority**: Admin ekranlarının (058) tek gerçek işlevi bu; MCP'ye taşınmadan söküm
programı ilerleyemez. Aynı zamanda seed'li admin OAuth istemcisi bu hikâyenin ön şartı — birlikte MVP.

**Independent Test**: Claude Desktop'a bağlan, admin hesabıyla gir; "ürünleri listele → şunun
detayını aç → fiyatını X yap → yayından kaldır → fiyat geçmişini göster" zinciri uçtan uca çalışır;
admin OLMAYAN hesap aynı tool'larda yetki hatası alır.

**Acceptance Scenarios**:

1. **Given** yönetim yetkili kullanıcı Claude Desktop'tan bağlı, **When** ürün listesini sayfalı/aramalı
   ister, **Then** stabil ürün kimlikli, sayfalama bilgili liste döner (yayında olmayanlar dahil).
2. **Given** listeden bir ürün kimliği, **When** detay istenir, **Then** künye + yayın durumu + bağlar
   (yazar/yayınevi/kategori) + stok görünür tek yanıtta döner.
3. **Given** bir ürün, **When** künye güncellenir, **Then** değişiklik kalıcıdır ve yanıt ürünün GÜNCEL
   hâlini içerir (agent ek çağrısız "yeni hâli" gösterebilir).
4. **Given** yayındaki bir ürün, **When** yayından kaldırılır, **Then** vitrin/keşif yüzeylerinden düşer
   (mevcut yayın-anahtarı davranışı korunur); tekrar yayına alınabilir.
5. **Given** fiyatı değişmiş bir ürün, **When** fiyat geçmişi istenir, **Then** değişiklik kayıtları
   (eski→yeni, zaman) döner.
6. **Given** yönetim scope'u OLMAYAN kullanıcı token'ı, **When** herhangi bir admin tool çağrılır,
   **Then** çağrı yetki hatasıyla reddedilir; veri sızmaz.
7. **Given** admin bir yazma tool'unu çağırdı, **When** işlem tamamlanır, **Then** kim/ne zaman/hangi
   işlem/hangi ürün bilgisi kalıcı denetim izinde görülebilir.

---

### User Story 2 - Admin stok yönetir (Priority: P2)

Admin, sohbetle bir ürünün stok adedini görür; mutlak değere ayarlar ya da artırır/azaltır.

**Why this priority**: 058'in ikinci ekran parçası; ürün yönetiminden bağımsız test edilebilir,
tek başına da değer taşır.

**Independent Test**: "Şu ürünün stoğu kaç? 25 yap. 3 azalt." zinciri Claude Desktop'tan çalışır;
sonuç stok-okuma tool'undan doğrulanır.

**Acceptance Scenarios**:

1. **Given** var olan ürün, **When** stok mutlak değere ayarlanır, **Then** yeni değer kalıcıdır ve
   yanıt güncel stoğu döner.
2. **Given** var olan ürün, **When** stok artırılır/azaltılır, **Then** mevcut davranış kurallarıyla
   (negatife düşürme reddi dahil) sonuç döner.
3. **Given** yönetim scope'suz token, **When** stok yazma çağrılır, **Then** yetki hatası.

---

### User Story 3 - Admin merchant kimliğini yönetir (Priority: P3)

Admin, ödeme gateway'i onboarding'i sonrası eline geçen merchant kimliğini (MerchantId + MerchantKey)
sohbetle kaydeder/durumunu görür (bugünkü Onboarding ekranı formunun muadili).

**Why this priority**: Nadir kullanılan ama sökümün ön şartı olan son admin formu; Docker reset
kurtarma yolu (bilinen operasyonel senaryo) bu tool'suz kapanmaz.

**Independent Test**: "Merchant kimliği tanımlı mı? Şu Id/Key ile kaydet" akışı Claude Desktop'tan
çalışır; sipariş çekim yolu yeni anahtarı kullanır.

**Acceptance Scenarios**:

1. **Given** kayıtlı merchant kimliği yok, **When** durum sorulur, **Then** "tanımsız" bilgisi döner
   (gizli alan sızmaz).
2. **Given** admin Id+Key verdi, **When** kaydetme çağrılır, **Then** kimlik kalıcıdır; sonraki çekimler
   bu kimliği kullanır; yanıtta anahtarın tamamı GERİ YAZILMAZ (maskeli özet).
3. **Given** yönetim scope'suz token, **When** kaydetme çağrılır, **Then** yetki hatası.

---

### User Story 4 - Müşteri agent'ı taksit seçeneklerini görür (Priority: P4)

Dış agent kullanan müşteri, sipariş öncesi "kayıtlı kartımla kaç taksit olur"u sorar; seçenekler
(taksit sayısı + toplam tutar) döner. Bugün bu bilgi yalnız ChatAgent'ın iç aracında — dış agent kör.

**Why this priority**: Müşteri paritesindeki son delik; ChatAgent sökümünde kaybolacak tek müşteri
yeteneği. Admin hattından bağımsız.

**Independent Test**: Claude Desktop'tan müşteri hesabıyla "sepetimdeki tutara taksit seçenekleri"
akışı; dönen seçenekler mevcut chat akışıyla (henüz yaşıyorken) karşılaştırılır.

**Acceptance Scenarios**:

1. **Given** sepeti dolu, kayıtlı kartlı müşteri, **When** taksit seçenekleri istenir, **Then**
   seçenek listesi (taksit sayısı + toplam) döner; kart sırrı/vault token yanıtta görünmez.
2. **Given** ödeme sağlayıcısına ulaşılamıyor, **When** taksit istenir, **Then** kullanıcı-dostu
   "şu an yapılamıyor" hatası döner (teknik ayrıntı sızmaz).
3. Ödeme sağlayıcısı (PaymentGateway) tarafında HİÇBİR değişiklik yapılmaz (kısıt).

---

### User Story 5 - Dış agent, rehber bilgisiyle kaliteli keşif yapar (Priority: P5)

Herhangi bir dış agent `query_storefront`'u kullanırken sorgu rehberini (şema kolonları, anlamsal
arama kalıbı, eşik, dürüstlük kuralları) tool'un KENDİSİNDEN öğrenir — bugüne dek bu bilgi yalnız
ChatAgent prompt'undaydı, dış agent'lar kör sorgu yazıyordu.

**Why this priority**: Mağazanın kalbi (metinle keşif) dış-agent dünyasında bu göç olmadan kördür;
ChatAgent sökümünün pazarlıksız ön şartı. Bağımsız test edilebilir.

**Independent Test**: TEMİZ bir Claude Desktop oturumundan (bizim prompt'suz) temalı arama ("kış
temalı bilim kurgu"), benzerlik ("buna benzer"), yazar/özellik filtresi ve sayfalama senaryoları
çalışır; 069 eval setinin dış-agent muadili geçer.

**Acceptance Scenarios**:

1. **Given** rehber göçü tamam, **When** dış agent tool tanımını okur, **Then** şema kolonları, sorgu
   kalıpları, anlamsal arama yer-tutucusu ve eşik kuralı tanımda mevcuttur.
2. **Given** temiz dış agent, **When** temalı arama ister, **Then** anlamsal kalıp doğru kullanılır ve
   alakalı sonuç döner (eşik kuralına uyulur).
3. **Given** şema tek-kaynağına kolon eklendi/silindi, **When** drift guard'ı koşar, **Then** rehberin
   YENİ evi ile şema arasındaki uyumsuzluğu yakalar (guard hedefi güncellenmiştir).
4. ChatAgent (henüz yaşarken) davranışı BOZULMAZ — mevcut chat E2E akışları geçer.

---

### Edge Cases

- Admin listede olmayan/silinmiş ürün kimliğiyle detay/güncelleme çağırırsa: bulunamadı hatası, iz
  bırakılır, veri değişmez.
- Aynı ürüne peş peşe iki güncelleme (agent tekrar denemesi): son yazan kazanır; yanıt hep güncel
  hâli döndürdüğünden agent farkı görür.
- Yazma tool'ları TEK ürün/tek kayıt işler — toplu (bulk) mutasyon tool'u YOKTUR; "tüm fiyatları
  düşür" tarzı istek agent tarafında N ayrı çağrıya döner ve her biri ayrı iz bırakır (bilinçli
  sürtünme, LLM kaza yarıçapını sınırlar).
- Admin OAuth istemcisi dinamik kayıtla (DCR) ELDE EDİLEMEZ; dış agent kayıt yolu yönetim scope'u
  isterse reddedilir (mevcut tavan aynen).
- Merchant anahtarı yanıtlarda/izlerde asla düz metin dönmez.
- Taksit tool'u sepet boş/kart yokken çağrılırsa: yönlendirici, kullanıcı-dostu hata.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem, yönetim yetkili kullanıcıya agent üzerinden ürünleri sayfalı + arama filtreli
  listeleme yeteneği SUNMALI; yanıt stabil ürün kimliği, yayın durumu ve sayfalama bilgisi içermeli.
- **FR-002**: Sistem, tek ürünün yönetim detayını (künye + bağlar + yayın durumu + fiyat + stok)
  tek çağrıda DÖNDÜRMELİ.
- **FR-003**: Sistem, tek ürünün künyesini güncelleme yeteneği sunmalı; başarılı yanıt ürünün güncel
  hâlini İÇERMELİ (agent ek çağrısız gösterebilmeli).
- **FR-004**: Sistem, tek ürünü yayına alma/yayından kaldırma yeteneği sunmalı; mevcut yayın-anahtarı
  davranışı (vitrinden düşme, geri alınabilirlik) korunmalı.
- **FR-005**: Sistem, ürünün fiyat değişiklik geçmişini sunmalı.
- **FR-006**: Sistem, tek ürünün stoğunu mutlak değere ayarlama ve artırma/azaltma yeteneği sunmalı;
  mevcut stok kuralları (ör. negatif reddi) değişmemeli.
- **FR-007**: Sistem, merchant kimliğini (Id + anahtar) kaydetme ve durumunu (tanımlı/maskeli özet)
  sorgulama yeteneği sunmalı; anahtar hiçbir yanıtta düz metin dönmemeli.
- **FR-008**: TÜM yönetim tool'ları yönetim scope'larıyla korunmalı; scope'suz çağrı veri sızdırmadan
  reddedilmeli. Mevcut kapalı scope kaydı (registry) yeni scope üretmeden kullanılmalı.
- **FR-009**: Her yönetim YAZMA çağrısı kalıcı denetim izi bırakmalı: kimlik, zaman, işlem, hedef
  kayıt, özet değişiklik (mevcut agent sorgu-izi emsali).
- **FR-010**: Yazma tool'ları tek-kayıt işlemeli; toplu mutasyon parametresi SUNULMAMALI.
- **FR-011**: Yönetim scope'lu, önceden tanımlı (seed) bir dış-agent OAuth istemcisi bulunmalı;
  dinamik kayıt (DCR) yolundan yönetim scope'u ALINAMAMALI — mevcut dış-agent scope tavanı değişmemeli.
- **FR-012**: Müşteri agent'ları, kayıtlı kart + sepet tutarı üzerinden taksit seçeneklerini
  sorgulayabilmeli; kart sırrı/vault token yanıtlarda görünmemeli; ödeme sağlayıcısında değişiklik
  YAPILMAMALI.
- **FR-013**: Vitrin sorgu rehberi (şema kolonları, sorgu kalıpları, anlamsal arama yer-tutucusu +
  eşik, dürüstlük/grounding kuralları) `query_storefront` tool tanımına taşınmalı; ChatAgent (henüz
  yaşarken) davranışı bozulmamalı.
- **FR-014**: Şema-rehber drift guard'ı rehberin yeni evini hedeflemeli; kolon ekleme/silme driftini
  yakalamaya devam etmeli.
- **FR-015**: Tool tanımları "arayüz metni" kalitesinde olmalı: alan açıklamaları, geçerli değerler,
  kullanım örnekleri — dış agent ek belge olmadan doğru kullanabilmeli.
- **FR-016**: DropShop onboarding başvurusu (submit/status) bizim yönetim tool'larımızla SARILMALI:
  admin tek MCP bağlantısından başvurur/durum sorar; DropShop'a makine kimliği sunucu içinde taşınır
  (admin token'ı dış realm'e gitmez); çağrılar bizim scope korumamız + denetim izimizden geçer.
  (Karar 2026-09-08: seçenek A — kendi tool'umuzla sarma.)

### Key Entities

- **Yönetim denetim izi**: her admin yazma işleminin kaydı — kim, ne zaman, hangi işlem, hedef,
  özet değişiklik. Salt-append; mevcut agent sorgu-izi ile aynı aile.
- **Seed'li admin istemcisi**: önceden tanımlı OAuth istemci kaydı; yönetim scope demeti; dinamik
  kayıt yüzeyinin DIŞINDA yaşar.
- **Merchant kimliği**: mevcut kayıt (Id + anahtar); bu spec yalnız agent yüzeyinden okunur/yazılır
  hâle getirir, modelini değiştirmez.
- **Taksit seçeneği**: taksit sayısı + toplam tutar çifti; geçici sorgu sonucu, kalıcı kayıt değil.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin, 058 ekranlarında yapabildiği HER işlemi (liste/ara, detay, künye, yayın, fiyat
  geçmişi, stok, merchant kimlik) yalnız Claude Desktop'tan uçtan uca tamamlayabilir — ekran açmadan.
- **SC-002**: Yönetim scope'suz hesapla denenen her admin tool çağrısı reddedilir; sızıntı sıfır.
- **SC-003**: Her admin yazma işlemi denetim izinde görünür; örneklem denetiminde eksik iz sıfır.
- **SC-004**: Temiz (prompt'suz) dış agent oturumunda 069 eval setinin dış-agent muadili en az
  mevcut chat başarı oranında geçer (referans: 13/13).
- **SC-005**: Müşteri dış agent'tan taksit seçeneklerini görebilir; mevcut chat akışıyla aynı
  seçenekler döner.
- **SC-006**: Dinamik kayıt (DCR) yolundan yönetim scope'u edinme denemesi %100 reddedilir.

## Assumptions

- Kayıt (SignUp) yüzeyi Identity.Server'da zaten mevcut (`Account/Create`; WebApp yalnız OIDC
  `prompt=create` tetikliyor) — taşıma GEREKMEZ, bu spec'in işi değil. Doğrulandı 2026-09-08.
- RBAC yönetim ekranları Identity.Server'da yaşar; WebApp sökümünden etkilenmez — kapsam dışı.
- Yönetim scope'ları mevcut kapalı kayıtta var (`catalog.write`, `stock.write`); merchant kimlik
  yazımı için scope seçimi plan aşamasında netleşir (yeni scope gerekirse registry yoluyla).
- WebApp admin ekranları ve admin REST uçları BU spec'te SÖKÜLMEZ; söküm ayrı feature (071 adayı).
  Bu spec süresince eski ekranlar çalışır kalır (çifte yüzey geçicidir).
- Tüketici referansı Claude Desktop'tır; ancak tool'lar standart MCP + OAuth konuşan her dış
  agent'la çalışmalıdır (istemciye özel davranış yok).
- Taksit sorgusu ödeme sağlayıcısının MEVCUT arayüzü üzerinden yürür; sağlayıcı tarafına dokunulmaz.
- "Generative UI" vizyonu gereği tıklama-etkileşimli sunucu şablonları (MCP Apps benzeri) bu spec'in
  kapsamı DIŞINDA; etkileşim konuşma üzerinden yürür. İleride ayrı feature olabilir.