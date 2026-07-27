# Feature Specification: IngestionAgent LLM-Sürücülü Yazıcılar

**Feature Branch**: `015-ingestion-llm-writers`

**Created**: 2026-07-26

**Status**: Draft

**Input**: User description: "IngestionAgent'ı deterministik (LLM'siz) MCP çağrılarından
gerçek LLM-sürücülü tool-calling'e taşıyan refactor."

**Artefakt kademesi: Tam.** Tek bileşen (IngestionAgent), yeni tablo/şema/aggregate/event
yok; ama gerçek bir teknik bilinmez (MAF conditional-edge + terminal semantiği) ve 007'nin
"NO LLM writers" duruşunun bilinçli tersine çevrilmesi var → şüphede üst kademe seçildi.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Feed değişikliği LLM-sürücülü agent'la yansır (Priority: P1)

Bir tedarikçi ürün snapshot'ı geldiğinde, sistem onu Catalog/Stock/Discount'a yansıtır;
ama bunu deterministik doğrudan çağrılarla değil, her servisin MCP tool'larını **bir LLM
agent'ının çağırması** yoluyla yapar. Böylece MCP gerçekten bir LLM tarafından kullanılır.

**Why this priority**: Feature'ın var oluş nedeni bu. LLM olmadan MCP anlamsız bir tören;
bu hikâye MCP'yi anlamlı kılan çekirdek davranıştır. Tek başına teslim edilince bile
sistem çalışır durumdadır (feed → doğru katalog/stok/indirim durumu).

**Independent Test**: Değişen bir feed kaydı yayınlanır; Catalog/Stock/Discount durumunun
snapshot'ı yansıttığı ve yazmaların LLM agent'ı üzerinden gittiği (log/trace) doğrulanır.

**Acceptance Scenarios**:

1. **Given** yeni bir ürün snapshot'ı, **When** feed yayınlar, **Then** LLM agent'ı
   `upsert_product` çağırır, ürün katalogda oluşur ve ProductId üretilir.
2. **Given** stok+indirimli bir snapshot, **When** feed yayınlar, **Then** sırayla stok
   `set_stock` ve indirim `set_product_discount` LLM agent'larıyla yazılır.
3. **Given** indirimsiz (DiscountPercent boş) bir snapshot, **When** işlenir, **Then**
   indirim adımı `remove_product_discount` çağırır ve etkisiz-başarı döner (idempotent).

---

### User Story 2 - Hata/retry/DLQ garantileri korunur (Priority: P1)

Bir adım (ör. stok) başarısız olduğunda, sonraki adımlar çalışmaz; mesaj mevcut kademeli
retry ardından DLQ yoluna, kayıt kimliği ve hata koduyla düşer. Dış davranış (at-least-once,
retry/DLQ) bugünküyle bire bir aynıdır.

**Why this priority**: Bu bir yazma yoludur; LLM'in getirdiği non-determinizm veri
bütünlüğünü tehdit eder. Garantiler korunmadan feature kabul edilemez — P1.

**Independent Test**: Bir servisi kapatıp feed yayınlanır; başarısız adımdan sonraki
adımların çalışmadığı, mesajın retry ardından DLQ'ya kimlik+hata ile düştüğü doğrulanır.

**Acceptance Scenarios**:

1. **Given** catalog adımı başarısız, **When** işlenir, **Then** stok ve indirim adımları
   **hiç çalışmaz** (LLM bile çağrılmaz) ve mesaj retry/DLQ yoluna girer.
2. **Given** stok servisi ısrarla kapalı, **When** retry tükenir, **Then** mesaj DLQ'ya
   kayıt kimliği + hata koduyla düşer.
3. **Given** run dış iptalle (execution timeout) yarım kalır, **When** terminal'e
   ulaşılmaz, **Then** run başarı sayılmaz; `WORKFLOW_INCOMPLETE` ile retry/DLQ tetiklenir.

---

### User Story 3 - Zarf-parse sürtünmesi kalkar, yazıcılar tekdüzeleşir (Priority: P2)

Her yazıcı adımı, tool sonucunu elle ayrıştırmak yerine küçük tipli bir sonuç alır; üç
adım da aynı LLM-agent iskeletini paylaşır (stok dahil, mimari tekdüzelik).

**Why this priority**: Refactor'ın ikincil ama somut kazancı: bakım kolaylığı ve tutarlı
yapı. Çekirdek davranıştan sonra gelir → P2.

**Independent Test**: Kod tabanında elle-yazılmış tool-zarf ayna tipleri (ToolOutcome vb.)
kalmadığı ve her yazıcının aynı sonuç sözleşmesini döndürdüğü doğrulanır.

**Acceptance Scenarios**:

1. **Given** bir yazıcı adımı, **When** tamamlanır, **Then** `(IsSuccess, ErrorMessage)`
   (catalog için ek `ProductId`) tipli bir sonuç döner; elle JSON zarf parse yoktur.
2. **Given** kod tabanı, **When** incelenir, **Then** eski deterministik tool-çağrı/parse
   makinesi (zarf ayna tipleri) tamamen kaldırılmıştır.

---

### Edge Cases

- **LLM sahte başarı raporlar** (yazma olmadı ama "ok" dedi): kalan risk. Idempotent
  replay + DLQ hafifletir; deterministik geri-okuma doğrulaması bu kapsamda **değil**
  (gelecekteki sertleştirme adayı — Assumptions'a bak).
- **Model sağlayıcı erişilemez/timeout**: adım başarısız → geçici hata retry penceresinde
  kurtulur; ısrarcıysa DLQ.
- **Değişmeyen feed kaydı**: Supplier.Gateway diff'ler → yayınlanmaz → ingestion tetiklenmez
  (davranış değişmez).
- **Kısmi yazma** (catalog yazıldı, stok patladı): tam replay idempotent tool'larla
  yakınsar; kısmi durum kendiliğinden iyileşir.
- **Başarısız kayıt Hangfire ile geri gelmez**: Gateway snapshot'ı atomik ilerletir;
  başarısızı yeniden süren tek şey Wolverine retry'dır (davranış değişmez).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem her `SupplierProductSnapshotReceived` mesajında snapshot'ı Catalog,
  Stock ve Discount'a, ilgili servis tool'larını **bir LLM agent'ına çağırtarak** yansıtmalı
  (deterministik doğrudan çağrı değil).
- **FR-002**: Sistem sabit adım sırasını Catalog → Stock → Discount korumalı.
- **FR-003**: Bir adım başarısız olursa sistem sonraki adımları **çalıştırmamalı** (LLM
  bile çağrılmamalı).
- **FR-004**: Herhangi bir adım hatası veya eksik run'da sistem, hatayı (kayıt kimliği +
  hata) mevcut kademeli-retry-ardından-DLQ yoluna taşımalı.
- **FR-005**: Sistem her yolda (başarı veya short-circuit) deterministik bir terminale
  ulaşmalı; eksik bir run **asla** sessizce başarı olarak ack'lenmemeli (S4 koruması).
- **FR-006**: ProductId'yi **Catalog** üretmeli; IngestionAgent ürün kimliği **üretmemeli**.
  Üretilen ProductId catalog adımından stok ve indirim adımlarına akmalı.
- **FR-007**: Yazmalar idempotent kalmalı (upsert / mutlak stok set / indirim set-veya-remove)
  ki mesaj replay'i yakınsasın.
- **FR-008**: Yazma yolu anonim kalmalı (kullanıcı token'ı yok).
- **FR-009**: Üç yazıcı adımının her biri yalnız kendi servisinin izinli tool'larına
  scope'lu kendi agent'ını kullanmalı (catalog: upsert; stock: set_stock; discount:
  set/remove).
- **FR-010**: Üç agent tek bir dil-modeli istemcisi/modelini (config'den) paylaşmalı;
  adım-başına model override'ı gelecekteki bir config genişletmesi **olabilir**.
- **FR-011**: Yazıcı adımları küçük tipli bir sonuç döndürmeli (başarı bayrağı + opsiyonel
  hata; catalog ayrıca ProductId); sistem tool sonuç zarfını **elle ayrıştırmamalı**.
- **FR-012**: Sistem, atıl deterministik tool-çağrı/parse makinesini (zarf ayna tipleri)
  kaldırmalı.
- **FR-013**: İndirim adımı agent yüzünde idempotent kalmalı (indirimsiz üründe remove =
  zararsız başarı).
- **FR-014**: Model yapılandırması (API anahtarı + model kimliği) config'den gelmeli;
  yokluğu açılışta hızlıca (fail-fast) hata vermeli.
- **FR-015**: MAF conditional-edge + terminal-collector + tamamlanma semantiği, ona
  güvenmeden önce uygulamanın **ilk adımında bir spike ile doğrulanmalı** (S4 emsali).

### Key Entities *(include if feature involves data)*

- **Tedarikçi ürün snapshot'ı**: kanonik mesaj — harici kimlik/SKU, ad, açıklama, fiyat,
  stok adedi, indirim yüzdesi, marka. (Mevcut kontrat; değişmez.)
- **Yazıcı sonucu**: bir adımın çıktısı — başarı bayrağı + opsiyonel hata mesajı; catalog
  varyantı ek olarak üretilen ProductId taşır.
- **Ingestion işi**: workflow boyunca akan geçici iş kaydı — mesaj + ProductId + hata/başarı
  durumu; kalıcı değil.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Başarılı işlenen değişen feed kayıtlarının %100'ünde Catalog/Stock/Discount
  durumu snapshot'ı yansıtır (mevcut davranışa göre regresyon yok).
- **SC-002**: Israrcı hataların %100'ü DLQ yoluna kayıt kimliği + hata nedeniyle düşer;
  sessiz başarı ack'i **0**.
- **SC-003**: Bir adım başarısız olduğunda o kayıt için çalışan sonraki adım sayısı **0**.
- **SC-004**: Yeniden teslim edilen (replay) bir kayıt, tek başarılı teslimatla **aynı**
  son duruma yakınsar (idempotent).
- **SC-005**: Tool sonuç-zarfı ayrıştırma kodu tamamen kaldırılmıştır (geriye **0** elle
  yazılmış zarf ayna tipi kalır).
- **SC-006**: Değişen her kayıt en çok **3** model-sürücülü yazma adımı tetikler; değişmeyen
  kayıt **0** tetikler (feed diff-only korunur).

## Assumptions

- ChatAgent'ın yerleşik "MCP tool'ları = AIFunction + ChatClientAgent" deseni yeniden
  kullanılır; ancak **anonim/token'sız** (PerUserMcpTool'un token makinesi yok → daha sade).
- Supplier.Gateway feed'i kanonikleştirip diff'lemeye devam eder; IngestionAgent girişi
  zaten temizdir (LLM'e ham veri normalizasyonu yüklenmez — bu kapsamda değil).
- **Tekdüzelik kararı**: stok adımının LLM'i pratikte karar vermese de (adet hazır) üç adım
  da LLM-sürücülüdür — bilinçli mimari/pedagojik tercih.
- **ProductId aktarımı = Seçenek A**: catalog Id'yi üretir ve kod tek Guid olarak taşır
  (agent'ların SKU ile çözmesi değil).
- Short-circuit, workflow conditional edge'leri + her yolda çalışan bir terminal collector
  ile gerçeklenir.
- Tek OpenAI-uyumlu model sağlayıcı kullanılır; feed diff-only olduğu için düşük hacimde
  maliyet/latency kabul edilebilir.
- Bu, 007'nin "NO LLM writers" duruşunun bilinçli tersine çevrilmesidir; dış davranış
  (yayınlanan yazmalar, retry/DLQ garantileri) korunur.
- **Kapsam dışı (gelecek adayı)**: LLM'in sahte-başarı riskine karşı deterministik
  geri-okuma doğrulaması; per-adım farklı model; feed ham veri normalizasyonunu LLM'e verme.