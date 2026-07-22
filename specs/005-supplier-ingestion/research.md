# Research: Tedarikçi Entegrasyonu (005)

Keşif 2026-07-22'de kod üzerinde yapıldı; tüm bulgular bu oturumda yeniden doğrulandı. NEEDS CLARIFICATION kalmadı.

## R1. Proje yerleşimi

- **Karar**: `src/services/supplier/Supplier.Api` (simülatör) + `src/agents/IngestionAgent` + `tests/IngestionAgent.Tests`.
- **Gerekçe**: Simülatör dış dünyadır, servis klasöründe ama BC değil; ingestion agent uygulamasıdır, ChatAgent emsali.
- **Alternatifler**: Tek proje (simülatör+ingestion) — sınırları bulanıklaştırır; mevcut bir servise gömme — BC ihlali.

## R2. Orkestrasyon: MAF Workflows (n8n değil)

- **Karar**: Akış kod içinde, Microsoft Agent Framework Workflows 1.13.0 ile; `Executor` + `WorkflowBuilder`.
- **Gerekçe**: Kullanıcı kararı (2026-07-22, n8n kararını geçersiz kılar); tip güvenliği, test edilebilirlik, repo içi yaşam.
- **Alternatifler**: n8n — dış bağımlılık, kod dışı akış; Wolverine saga — agent kavramı yok, öğrenme hedefine uymaz.
- **API notu**: `ReflectingExecutor` obsolete; `Executor<TIn,TOut>` + `ConfigureProtocol` kullanılır. Koşullu edge,
  fan-out/fan-in, `WithOutputFrom`, `InProcessExecution.RunAsync` mevcut. Paket sürümleri Directory.Packages.props'ta hazır.

## R3. İdempotency: SHA-256 hash kapısı

- **Karar**: Ara modelin kanonik JSON'u üzerinden SHA-256; anahtar `tedarikçi + harici kimlik`. Karar saf koddadır.
- **Gerekçe**: FR-012/FR-014 — deterministik, LLM'e sorulmaz; hash değişmediyse kayıt hiçbir aşamaya girmez.
- **Alternatifler**: Alan-alan karşılaştırma — eşdeğer ama gürültülü; LLM'e sorma — spec yasaklıyor.

## R4. Staging deposu

- **Karar**: Marten dokümanları `ingestionDb`/`ingestionManagement`: `StagingRecord` (Id = "{supplier}:{externalId}") + `IngestionRun`.
- **Gerekçe**: "Her context kendi veritabanı" ilkesi; Marten zaten standart; deterministik Id upsert'ü basitleştirir.
- **Alternatifler**: Domain servislerinin DB'sine yazmak — BC ihlali; dosya/queue — sorgulanabilirlik (FR-023) kaybolur.

## R5. Domain'e yazım: MCP + M2M client

- **Karar**: IdS'e `ingestion.agent` client'ı (client_credentials; `catalog.write stock.write discount.write`).
  Token deseni WebApp `TokenService` emsali; MCP çağrısına token enjekte eden yeni M2M handler yazılır.
- **Gerekçe**: FR-019 + mevcut kimlik altyapısının yeniden kullanımı (spec varsayımı); `m2m.client` şablon olarak hazır.
- **Alternatifler**: Servis API'lerini doğrudan çağırmak — MCP-as-contract duruşuna aykırı; DB'ye yazmak — yasak.

## R6. Yazıcı agent'lar: agent başına tek MCP

- **Karar**: Üç agent: CatalogAgent (catalog MCP), StockAgent (stock MCP), DiscountAgent (discount MCP). Singleton.
- **Gerekçe**: Kullanıcı kararı; net sorumluluk, tool allowlist'i daraltır, izole test kolaylaşır.
- **Alternatifler**: Tek agent üç MCP — tool karmaşası; agent'sız düz MCP çağrısı — feature'ın öğrenme hedefini boşaltır.

## R7. Agent yanıt zarfı

- **Karar**: Katı JSON zarf. Catalog: `{status: created|updated|failed, productId, error}`;
  Stock/Discount: `{status: ok|failed, error}`. Parse edilemeyen yanıt = kayıt Failed.
- **Gerekçe**: LLM çıktısı programatik akışa bağlanmalı; belirsiz metin akışı bozamaz (FR-020).
- **Alternatifler**: Serbest metin + ikinci LLM ile yorum — pahalı ve kırılgan.

## R8. Stok yazımı

- **Karar**: Yeni `SetStock` command + `ProductStock.SetQuantity` davranışı + `set_stock` MCP tool.
  Yeni üründe stok `CreateProductCommand.InitialStock` + `ProductCreatedEvent` ile oluşur; StockAgent yalnız değişimde çalışır.
- **Gerekçe**: Stock'ta yalnız Increase/Decrease var; feed mutlak adet verir, set semantiği şart.
- **Alternatifler**: Fark hesaplayıp Increase/Decrease — kırılgan; event'e stok güncelleme eklemek — event şişer.

## R9. İndirim yazımı

- **Karar**: DiscountAgent, yüzdeyi `set_product_discount` (yeni MCP tool, mevcut `SetProductDiscountCommand` sarmalar) ile
  yazar; feed'den indirim kalkarsa `remove_product_discount` (yeni tool, `RemoveProductDiscountCommand`) çağrılır.
  İndirim kodu domain'e yazılmaz; StagingRecord'da kampanya etiketi olarak kalır.
- **Gerekçe**: Kullanıcı kararı (2026-07-22). Discount aggregate'i ürün+oran modelidir, kod alanı yoktur; kupon ayrı feature.
- **Alternatifler**: Kupon kavramını geri eklemek — kapsamı Discount context'ine büyütür; staging-only — kullanıcı yazım istedi.

## R10. Marka eşleme

- **Karar**: Deterministik, case-insensitive `BrandType` enum eşlemesi (normalizasyon adımında, adapter sonrası).
  Eşlenemeyen marka → kayıt Failed (FR-018). Veriler temiz geldiğinden alias tablosu gerekmiyor.
- **Gerekçe**: Catalog `BrandType` enum'u tedarikçi kataloglarını birebir kapsıyor (Apple..Xiaomi); temiz veri kararı.
- **Alternatifler**: LLM ile eşleme — deterministik olmalı; alias tablosu — kirli veri yok, YAGNI.

## R11. SKU üretimi

- **Karar**: `CreateProductCommand.Sku` için deterministik değer: harici kimlik aynen kullanılır (ör. `ACM-1001`).
- **Gerekçe**: Feed'de SKU yok; harici kimlik zaten tedarikçi-öneki taşıyan benzersiz bir koddur.
- **Alternatifler**: `{SUPPLIER}-{externalId}` birleşimi — kimlikte önek zaten var, çift önek üretir.

## R12. Veri setleri ve temizlik

- **Karar**: Kanonik veri `Supplier.Api/Datasets/*.json` (kullanıcı hazırlar); açılışta `supplierDb`'ye seed edilir.
  Uçlar formatı isteğe göre render eder (JSON/CSV/XML). Veriler temiz/tekdüze; bozuk kayıt yalnız eksik alanla simüle edilir.
- **Gerekçe**: Kullanıcı kararı (2026-07-22): format pisliğiyle uğraşılmayacak; tek kanonik dosya formatı bakımı kolaylaştırır.
- **Alternatifler**: Format-özgü dataset dosyaları — üç ayrı el bakımı; kirli veri — kullanıcı istemedi.

## R13. Catalog SeedData

- **Karar**: `Infrastructure/SeedData.cs` ve `Program.cs` kaydı tamamen silinir.
- **Gerekçe**: Kullanıcı kararı (2026-07-22): "SeedData istemiyorum"; katalog yalnız ingestion'dan beslenir.
- **Alternatifler**: `Seed:Enabled` bayrağı — reddedildi; olduğu gibi bırakmak — seed + feed ürünleri karışırdı.

## R14. Ingestion API yetkisi

- **Karar**: Ingestion uçlarının tamamı şimdilik anonim; `ingestion.write` scope'u ertelendi (kullanıcı kararı, 2026-07-22).
  Domain yazımları yine korunur: agent'lar `ingestion.agent` M2M token'ıyla scope'lu command'ları çağırır.
- **Gerekçe**: Tetikleme ucu domain'e doğrudan yazmaz; asıl yazımlar zaten `[RequiredScope]` arkasında. Geliştirme sadeliği.
- **Alternatifler**: `ingestion.write` scope'u — ertelendi, ileride tek `.RequireAuthorization` satırıyla eklenebilir.

## R15. Eşzamanlılık (tek run)

- **Karar**: Süreç içi kilit (atomik bayrak/SemaphoreSlim); run sürerken ikinci tetikleme HTTP 409 ile reddedilir.
- **Gerekçe**: FR-024; tek instance varsayımıyla süreç içi kilit yeterli, dağıtık kilit YAGNI.
- **Alternatifler**: DB tabanlı kilit — çok instance yok; kuyruklama — spec "reddet"e izin veriyor, basit olan seçildi.

## R16. Parse bağımlılıkları

- **Karar**: Yeni paket yok: JSON = System.Text.Json/Newtonsoft, CSV = elle split (biçim bizim), XML = XDocument.
- **Gerekçe**: Biçimler kendi kontrolümüzde ve temiz; CsvHelper vb. eklemek CPM'e gereksiz yük.
- **Alternatifler**: CsvHelper — tek temiz CSV için ağır.

## Doğrulanan mevcut durum (2026-07-22)

- Catalog: `CreateProductCommand(Name,Desc,Price,Sku,Brand,ImageUrl?,InitialStock)` + `UpdateProductCommand` var;
  MCP'de yalnız `get_product`/`search_products` — yazma tool'ları eklenecek.
- Stock: yalnız `IncreaseStock`/`DecreaseStock`; `ProductCreatedEvent` handler'ı idempotent stok kaydı açıyor.
- Discount: `SetProductDiscountCommand(ProductId, Rate)` + `RemoveProductDiscountCommand(ProductId)` hazır,
  `DiscountRate` 0–100 doğruluyor, ikisi de `[RequiredScope(DiscountWrite)]`; MCP'de yalnız `get_discount_by_product`.
- IdS `Config.cs`: `m2m.client` şablonu client_credentials için hazır; scope'lar `AuthorizationScopes`'ta merkezi.
- ChatAgent emsalleri: named HttpClient'lar, `TokenInjectingHandler`, `McpToolProvider` + allowlist, Singleton agent.