# Research: Query Storefront — Tek Serbest-Sorgu Kapısı

**Feature**: 069 | **Date**: 2026-09-07

Kaynaklar: brainstorm + spike (memory `069-query-storefront-direction`, 2026-09-07), 067 kod tabanı
keşfi (Storefront.Api, ChatAgent), spec Assumptions'taki kilitli kararlar. Belirsizlik kalmadı.

## R1 — View + rol kurulumu: startup bootstrap servisi (Weasel'a EMANET DEĞİL)

- **Decision**: `AgentQuerySurfaceBootstrap` (IHostedService, `AddMarten` SONRASI kayıt): idempotent
  `CREATE OR REPLACE VIEW storefrontmanagement.storefront_sellable` + `DO $$ ... CREATE ROLE IF NOT
  EXISTS`-eşdeğeri rol + `GRANT USAGE ON SCHEMA` + `GRANT SELECT` YALNIZ view'a. DDL, kolon listesini
  `StorefrontSellableSchema` tek-kaynağından üretir.
- **Rationale**: Spike kanıtı — Weasel elle eklenen sütunu her açılışta siliyor (tablo kendi yönetimi);
  ayrı VIEW ise Weasel'ın sahiplenmediği nesnedir, dokunmaz. Marten `IFeatureSchema` yolu API
  arkeolojisi ister (spike'ta `VectorOn` öksüz çıktı — aynı sınıf risk); hosted-service emsali zaten
  var (`EmbeddingBackfillService`, şemaya startup'ta dokunuyor, kanıtlı çalışır). Sıralama: hosted
  service'ler kayıt sırasıyla koşar; Marten `ApplyAllDatabaseChangesOnStartup` önce biter.
- **Alternatives**: Weasel `FeatureSchemaBase` (belgesiz/riskli API yüzeyi, spike dersi — red); elle
  migration script (repo'da migration komutu YOK, CLAUDE.md — red).

## R2 — Zırh mimarisi: 3 katman; kısıtlı DB rolü ZORUNLU (opsiyonel değil)

- **Decision**: (1) **Saf bekçi** `AgentSqlGuard` — yorum soyma, tek statement (`;` reddi), yalnız
  `SELECT`/`WITH` başlangıcı, yasak kelime listesi (INSERT/UPDATE/DELETE/DROP/ALTER/CREATE/GRANT/
  COPY/TRUNCATE/DO/EXECUTE/SET/pg_sleep/pg_read/dblink...), FROM/JOIN ilişki taraması → whitelist
  yalnız `storefront_sellable`, SQL uzunluk tavanı. (2) **Kısıtlı DB rolü** — ayrı `NpgsqlDataSource`
  (storefrontDb conn-string'inden kullanıcı/şifre `AgentQueryOption`'dan değiştirilerek kurulur);
  rolün TEK yetkisi view SELECT'i. (3) **Çalıştırma sınırları** — `SET LOCAL statement_timeout` +
  LIMIT sarmalama (R4).
- **Rationale**: FR-005 "yapısal olarak" der — regex bekçisi tek başına yapısal değildir (kaçırdığı
  desen `mt_doc_userpurchase`'a ulaşır); rol katmanı bunu Postgres yetki düzeyinde İMKANSIZ kılar
  (planner, yürütme öncesi permission-denied verir → FR-004 "çalıştırılmadan" ruhu korunur). Bekçi
  yine önde durur: ucuz, log'a NET ret sebebi yazar, kötücül sorguların çoğu DB'ye hiç gitmez.
  Memory "rol OPSİYONEL" diyordu — FR-005'in yapısal şartı gereği plan'da ZORUNLU'ya yükseltildi.
- **Alternatives**: Tam SQL parser (libpg_query .NET bağlaması — yeni native bağımlılık, 20k-satırlık
  dev sahnesi için aşırı, red); yalnız rol + bekçisiz (ret sebebi opak, asistan düzeltme döngüsü
  körleşir, red); EXPLAIN ön-uçuşu (ekstra tur, timeout'la kısmen çakışık, YAGNI — red).

## R3 — `{{EMBED:"metin"}}`: guard SONRASI, çalıştırma ÖNCESİ; parametre olarak bind

- **Decision**: `EmbedPlaceholder` saf ayrıştırıcı: `{{EMBED:"..."}}` geçişlerini bulur, her birini
  parametre yer-tutucusuyla (`CAST(@embN AS vector)`) değiştirir, metinleri çıkarır. Akış: ayrıştır →
  ikameli SQL'i bekçiden geçir → GEÇERSE metinleri `IEmbeddingGenerator`'a ver (067 singleton'ı) →
  vektör-literal string bind (067 `ToVectorLiteral` fix'i taşınır: Weasel `Pgvector.Vector` bind
  edemiyor, metin-literal + CAST şart). Birden çok yer-tutucu desteklenir.
- **Rationale**: Reddedilecek sorguya OpenAI parası/gecikmesi harcanmaz; LLM vektör mekaniği görmez
  (kilitli karar); parametre bind'i SQL-injection yüzeyini embedding metnine kapatır.
- **Alternatives**: Embedding'i SQL'e literal gömme (LLM'e vektör sızar + log şişer, red); embed'i
  guard'dan önce yapmak (boşa maliyet, red).

## R4 — Satır tavanı: her sorgu alt-sorguya sarılır

- **Decision**: Bekçiden geçen SQL `SELECT * FROM ( <sql> ) _q LIMIT {MaxRows+1}` olarak sarılır
  (WITH dahil — Postgres alt-sorguda CTE'ye izin verir). `MaxRows+1` döndüyse yanıtta
  `Truncated=true` + satırlar MaxRows'a kırpılır → asistan "toplamı daralt/sayfala" davranışına geçer
  (spec edge-case). LLM kendi `LIMIT/OFFSET`'ini yazabilir (sayfalama kalıbı prompt'ta); sarmalayıcı
  yalnız TAVAN garantisidir.
- **Rationale**: Tavanı LLM'in yazdığı LIMIT'e emanet etmek güvence değildir (FR-004 üst-sınır şartı);
  sarmalama deterministik ve sorgudan bağımsızdır. İç ORDER BY alt-sorguda korunur (tavan kırpması
  zaten "sonuç büyük" sinyalidir, sıra hassasiyeti sayfalama kalıbına aittir).
- **Alternatives**: SQL'de LIMIT var mı diye ayrıştırıp değiştirme (kırılgan, red); cursor/fetch-size
  ile kısmi okuma (aynı etki, daha çok kod, red).

## R5 — Yanıt şekli: kolon-adlı JSON satırları (sabit Item DTO'su YOK)

- **Decision**: `QueryStorefrontForAgent.Response`: `Ok`, `Rows` (`List<Dictionary<string,object?>>` —
  DataReader'dan kolon adı→değer), `RowCount`, `Truncated`, ret/hata durumunda `MessageItem.Code`
  (`StorefrontResourceConstants.AgentSql*` sabitleri) + kısa makine-okur açıklama (asistanın düzeltme
  döngüsü için, FR-007). `vector` tipli kolonlar yanıttan AYIKLANIR (embedding asla LLM'e dönmez).
- **Rationale**: Serbest SELECT listesi sabit DTO ile temsil edilemez (aggregation/istatistik sorguları
  keyfi kolon üretir); kolon-adlı satır LLM'in doğal okuduğu şekildir. Embedding ayıklaması hem token
  israfını hem vektör sızıntısını keser.
- **Alternatives**: Ham Postgres hata metnini aynen dönme (sürüm/iç-şema sızıntısı; kod + budanmış
  mesaj yeter — kısmen red: SQLSTATE + tek satır mesaj korunur, iz log'unda tam metin durur).

## R6 — Sorgu izi: `AgentQueryLog` Marten dokümanı

- **Decision**: Her çağrı (ret DAHİL) `AgentQueryLog` yazar: `Id`, `Sql` (EMBED ikamesi ÖNCESİ ham
  metin), `Verdict` (Executed/Rejected/Failed), `RejectCode?`, `ErrorDetail?` (tam Postgres mesajı),
  `RowCount`, `DurationMs`, `CreatedAt`. Yazım ana oturumla ayrı hafif `IDocumentSession` ile (sorgu
  kısıtlı bağlantıda, log SAHIP bağlantıda — rol view-dışına yazamaz zaten).
- **Rationale**: FR-006 + SC-005 kalıcı, sorgulanabilir iz ister; ILogger satırı tek başına
  "kayıtlardan bulunabilir" şartını zayıf karşılar. Marten dokümanı ek altyapı istemez.
- **Alternatives**: Yalnız yapısal log (arama/inceleme zayıf, red); ayrı log tablosu ham SQL ile
  (Marten dokümanı varken elle DDL gereksiz, red).

## R7 — Anlamsal eşik: prompt kalıbına gömülür; `SemanticSearchOption` silinir

- **Decision**: 0.68 eşiği (067 kalibrasyonu) ChatAgent prompt'undaki kNN/benzerlik kalıplarına
  literal yazılır ("`embedding <=> ... < 0.68` altı yoksa 'bulunamadı' de"); `SemanticSearchOption`
  tek tüketicileri silinen slice'lar olduğundan kalkar. Dürüstlük güvencesi eval'de iki senaryoyla
  (alakasız sorgu, filtreli-benzerlik boş küme) sabitlenir.
- **Rationale**: Eşiği config'te tutup prompt'a taşıyacak köprü yok (prompt derleme-zamanı sabiti);
  iki kopya = drift. Tek kopya prompt'ta + eval bekçiliği. Kalibrasyon değişimi = prompt satırı değişimi.
- **Alternatives**: Tool'un SQL'e eşik enjekte etmesi (keyfi SQL'de güvenilir enjeksiyon noktası yok,
  red); config + drift-guard script'ine eşik kontrolü eklemek (script'i kırılganlaştırır, şimdilik red).

## R8 — Şema-prompt drift guard'ı: tek kaynak + script

- **Decision**: `StorefrontSellableSchema` (kolon adı+PG tipi+tek satır açıklama) TEK kaynak: (a)
  bootstrap DDL'i buradan üretilir; (b) ChatAgent prompt'undaki şema bloğu bu listeden ELLE yazılır;
  (c) `scripts/check-agent-query-schema.sh` (emsal: `check-flow-links.sh`) şema sınıfındaki her kolon
  adının ChatAgent `ConstValues.cs`'te geçtiğini doğrular — kolon ekleme/silme/yeniden adlandırma
  driftini yakalar.
- **Rationale**: View'ı kod üretir ama prompt'u üretemez (ayrı süreç, derleme sabiti) → guard script
  ev-içi kanıtlı desen. Kabul edilen sınır: tip/açıklama driftini yakalamaz (ad yeter — kolon adı
  değişmeden anlamı değişmez pratikte).
- **Alternatives**: Prompt'u runtime'da şemadan üretmek (ChatAgent→Storefront derleme bağımlılığı
  doğurur, ajan/servis sınırı bulanır — red); guard'sız (069 itiraz kaydının kabul görmüş maddesi
  guard'dı — red).

## R9 — View kolon tasarımı: LLM-ergonomik düz tipler; FK id'leri ATILIR

- **Decision**: Kolonlar data-model.md'de. Kritik seçimler: `authors text[]` (yalnız adlar —
  `unnest`/`ILIKE ANY` ile filtre-karşılaştırma kolay; AuthorId'nin tek-ilişki dünyasında sorgu değeri
  yok); `publisher`/`category` düz text (id'siz); `specs jsonb` (Key/Value çiftleri —
  `jsonb_array_elements` kalıbı prompt'ta); `product_id uuid` KALIR (sepete ekleme/benzerlik/kendisi-
  hariç için şart); `added_at` = `mt_last_modified` (yaklaşıklık spec'te kabul); `embedding vector`
  LEFT JOIN ile (embedding'siz kitap satırı kaybolmaz; kNN'de NULL kendiliğinden elenir).
- **Rationale**: LLM'in SQL isabeti düz kolonla artar (jsonb path zorunluluğu hata sınıfı üretir —
  yalnız `specs`te kalır, o da doğası gereği çift-listesi); ad-bazlı eşleşme ILIKE ile yazım-varyantına
  dayanıklı (068'in id çözümleme derdi bu modelde sorgu kalıbına iner — spec Assumption).
- **Alternatives**: authors jsonb aynen (LLM'e `->>'Name'` cambazlığı, red); ayrı author satır-patlatma
  view'ı (ikinci ilişki = "tek yüzey" deneyimini bozar, red); id kolonlarını tutmak (kullanılmayan
  yüzey = prompt gürültüsü, red).