# Research: Supplier Gateway + State'siz Ingestion

Tasarım kararları kullanıcıyla oturumda tartışılarak verildi; bu dosya kararları ve gerekçeleri sabitler.

## R1 — Gateway'in yeri ve kimliği

- **Decision**: `src/services/supplier/Supplier.Gateway`; Aspire resource `supplier-gateway`;
  DB `supplierGatewayDb`; Marten şeması `supplierGatewayManagement`.
- **Rationale**: Sınır bileşeni; tedarikçi sınırının parçası olarak Supplier.Api'nin yanında durur.
  LLM/MCP içermediği için `src/agents` yanlış ev olurdu.
- **Alternatives considered**: `src/agents` altında tutmak (agent değil, reddedildi);
  Supplier.Api içine gömmek (maket ile gerçek bileşen karışır, reddedildi).

## R2 — Kanonik mesaj ve adlandırma

- **Decision**: `Shared.IntegrationEvents.SupplierProductSnapshotReceived(SupplierCode, ExternalId,
  Name, Description, Brand, Price, StockQuantity, DiscountPercent?)`. `RabbitMqConstants.SupplierProductSnapshot`:
  Exchange `supplier.product-snapshot`, Queue `ingestion.supplier-product-snapshot`, DLQ `...-snapshot.dlq`.
- **Rationale**: Tek kanonik tip; tedarikçi kimliği alan olarak taşınır (kullanıcı kararı).
  Snapshot vurgusu "diff değil, güncel hal" anlamını isimde taşır.
- **Alternatives considered**: Tedarikçi başına event tipi (kontrata format sızdırır, reddedildi);
  kaygı başına bölme (ikinci tedarikçiye ertelendi, spec varsayımı).
- **Not**: Feed'deki `DiscountCode` kontrata alınmaz — bugünkü yazım yolu yalnız `rate` kullanıyor
  (`DiscountWriterAgent.SetAsync`). Gateway kıyası kanonik model üzerinden yapılır; kod alanı YAGNI.

## R3 — Gateway değişiklik kapısı ve sıralama

- **Decision**: `FeedSnapshot` dokümanı (Id = ExternalId, içerik = kanonik kayıt). Kayıt başına:
  yoksa yayınla+kaydet; aynıysa hiçbir şey; farklıysa yayınla+üstüne yaz. Sıra: önce publish, sonra save.
  Save kayıt başına yapılır (mükerrer penceresi tek kayıt). Feed içi mükerrer ExternalId: ilki kazanır.
- **Rationale**: Çökmede "kaybetmektense tekrarla" (kullanıcıyla netleşen karar); tekrar zaten zararsız.
  Record değer eşitliği kıyası 005'ten miras (hash'siz `==`).
- **Alternatives considered**: Batch sonunda tek SaveChanges (mükerrer penceresi büyür, kazanım önemsiz);
  önce kaydet sonra yayınla (çökmede kayıt kaybolur, kabul edilemez).

## R4 — Gateway zamanlama ve tetik

- **Decision**: `PeriodicTimer`'lı BackgroundService (30 dk, config ile değiştirilebilir) + manuel
  `POST /v1/feeds/pull` (202/409). Üst üste binme süreç içi `SemaphoreSlim(1,1)` ile engellenir.
- **Rationale**: Eski IngestionScheduler deseninin ait olduğu yere taşınmış hali; kanıtlanmış desen.
- **Alternatives considered**: Quartz/Hangfire (tek periyodik iş için ağır, reddedildi).

## R5 — Agent tüketimi ve workflow şekli

- **Decision**: IngestionAgent'a Wolverine + RabbitMQ girer: `ListenToRabbitQueue(ingestion...)`.
  Handler mesaj başına MAF workflow koşar. Workflow üç zincirli executor olur:
  CatalogWrite → StockWrite → DiscountWrite; `RecordJob` yalnız mesaj + ara sonuçları taşır.
- **Rationale**: StagingGate ölünce iki-executor şekli anlamını yitirir; yazıcı başına executor,
  MAF Workflows kimliğini korur ve akış sırasını görünür kılar.
- **Alternatives considered**: Tek DomainWriteExecutor (workflow tek düğüme iner, MAF anlamsızlaşır);
  MAF'ı tamamen bırakmak (feature'ın varlık sebebiyle çelişir, reddedildi).

## R6 — Retry / DLQ politikası

- **Decision**: Kuyruk durable + DLQ tanımlı. Politika: exception'da kademeli bekleme ile sınırlı
  retry (ör. 3 deneme), tükendiğinde `MoveToErrorQueue` → DLQ. Handler, başarısız `ToolOutcome`/Result'ı
  `IngestionWriteException`'a çevirerek politikayı tetikler.
- **Rationale**: Wolverine hata politikaları exception-tabanlı; Result dönen handler mesajı ack'ler.
  Dönüşüm tek sınır noktasında, gerekçesi plan Complexity Tracking'de.
- **Alternatives considered**: Handler'da elle nack/requeue (altyapıyı yeniden yazmak, reddedildi);
  sonsuz retry (zehirli mesaj kuyruğu kilitler, reddedildi).

## R7 — Discount idempotent remove'un yeri

- **Decision**: Yalnız agent'a açık slice (`Features/Agent/RemoveProductDiscount`) NotFound'u `Ok`'a
  çevirir. Domain command ve REST DELETE (404) davranışı değişmez.
- **Rationale**: İdempotent semantik makine tüketicisinin ihtiyacı; insan yüzünde 404 anlamlı kalır.
  MCP tool zaten agent slice'ını sardığı için değişiklik tek noktada.
- **Alternatives considered**: Domain command'ı Ok yapmak (REST kontratını sessizce değiştirir, reddedildi).

## R8 — Yeni üründe stok yazımının atlanması (korunur)

- **Decision**: `upsert_product` "created" dönerse `set_stock` çağrılmaz; stok `initialStock` ile
  `ProductCreatedEvent` üzerinden açılır. "updated" dönerse stok mesajdaki miktara eşitlenir.
- **Rationale**: 005'in R8 kararı geçerli; karar artık senkron tool cevabından, state'siz verilir.
- **Alternatives considered**: Her zaman set_stock (create yolunda event ile yarışır, gereksiz çift yazım).

## R9 — Silinenler ve AppHost yeniden bağlama

- **Decision**: Agent'tan silinen: StagingRecord, IngestionRun, FeedRecord, FeedClient,
  IngestionScheduler, IngestionRunService, StagingGateExecutor, IngestionEndpoints, Marten/ingestionDb.
  AppHost: `ingestionDb` kalkar; `supplierGatewayDb` + `supplier-gateway` (supplier-api + rabbit + db
  referanslı) eklenir; `ingestion-agent` rabbit + catalog/stock/discount referanslı, DB'siz kalır.
  `SchemaConstants.IngestionSchemaName` silinir, `SupplierGatewaySchemaName` eklenir.
- **Rationale**: Spec US4/FR-017; run API'sinin yerini kuyruk/DLQ görünürlüğü alır (kullanıcı kararı).
- **Alternatives considered**: Run API'sini geçici tutmak (beslendiği veri kalmıyor, reddedildi).

## R10 — Test stratejisi

- **Decision**: Yeni `tests/Supplier.Gateway.Tests`: FeedSnapshot kapı kararları (yok/aynı/farklı,
  mükerrer eleme). IngestionAgent.Tests: StagingRecordTests silinir; yerine saf yazım-planı kararı
  testleri (created→stok atla, updated→stok yaz, yüzde boş→remove) gelir.
- **Rationale**: Anayasa kalite kapısı: saf domain birim testleri; broker/MCP entegrasyonu quickstart
  ile canlı doğrulanır (mevcut pratiğe uygun).
- **Alternatives considered**: Testcontainers ile broker entegrasyon testi (repo pratiğinde yok, ertelendi).