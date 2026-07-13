# Implementation Plan: Product Enrichment Agent

**Branch**: `002-product-enrichment-agent` | **Date**: 2026-07-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-product-enrichment-agent/spec.md`

## Summary

Yeni bir WebApi agent projesi (`ProductEnrichmentAgent`) bir BackgroundService ile
eksik (not-on-sale) ürünleri Catalog'dan MCP ile çeker. Ürün başına, iki agent'ı
sıralı işleten bir Agent Framework **Workflow** çalışır: (1) Description agent'ı
ad+markadan açıklama üretir; (2) Image agent'ı üründen bir görsel prompt'u kurar ve
OpenAI image API ile gerçek bir görsel üretir. Görsel File.Api'ye MCP ile yüklenir
(File `Images/`'a yazar, servable URL döner); açıklama ve URL Catalog'a MCP yazma
tool'larıyla (yalnızca boş alan) geri yazılır ve ürün satışa çıkar.

## Netleştirme: agent yalnız MCP orkestratörü

Agent **stateless bir orkestratördür**; kendi deposu/DB'si/outbox'ı yoktur. AI üretimi
(LLM + image) dışında, BC'lere dokunan her adım bir **MCP tool çağrısıdır**. Bu yüzden
dayanıklılık ve idempotency garantileri agent'ta değil, **çağrılan servislerin MCP tool
handler'larında** yaşar (anayasa: MCP tool'ları ince sarmalayıcı, iş mantığı handler'da):

- **Görsel ProductId-idempotent** → File.Api `upload_product_image` handler'ı (dedupe).
- **Açıklama/URL boşsa-yaz** → Catalog `SetDescriptionIfEmpty`/`SetImageUrlIfEmpty`.
- **Geçici hata retry** → agent orkestrasyonu aynı MCP çağrısını backoff'la tekrarlar.
- **Dayanıklı iş kuyruğu** → Catalog'daki eksik ürünler kümesi (her koşu yeniden taranır).

Sonuç: **outbox gerekmez** — dayanıklılık zaten servis tarafında ve Catalog'un eksik-ürün
(desired-state) kümesindedir; agent yalnız doğru MCP çağrılarını sırayla yapıp retry eder.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Microsoft.Agents.AI + Microsoft.Agents.AI.Workflows (Agent
Framework, Workflow), Microsoft.Extensions.AI, OpenAI SDK (chat: gpt-4o-mini; image:
gpt-image-1), ModelContextProtocol (MCP client + File.Api'de server), Marten, Wolverine;
SixLabors.ImageSharp (File.Api'de 256×256 resize).

**Storage**: Agent stateless (DB yok). File.Api görseli **yalnız dosya sistemine**
(`Images/{ProductId}.png`, **256×256**) yazar — bu feature için yeni aggregate/Marten yok;
idempotency dosya varlık kontrolüyle. Catalog `Product`'ı Marten `catalogDb`'de günceller.
(Legacy Marten/`fileDb` + course-picture akışı bağlı kalır; sökümü ayrı bir cleanup.)

**Testing**: xUnit + Shouldly; saf domain birim testleri (Product davranış metotları).
Agent/Workflow ve File upload entegrasyonu quickstart ile canlı doğrulanır.

**Target Platform**: Linux/container, Aspire AppHost altında bir resource.

**Project Type**: Distributed microservices; yeni bir agent (worker+web) projesi.

**Performance Goals**: 30 seed ürünlük tek toplu koşu makul sürede biter; OpenAI
image API oran sınırları içinde kalır (ürünler sıralı/az-eşzamanlı işlenir).

**Constraints**: Bounded context sınırı sert — agent yalnız MCP sözleşmeleriyle yazar,
hiçbir DB'ye dokunmaz. Kısmi başarı: bir alan başarısızsa ürün eksik kalır.

**Scale/Scope**: 30 eksik ürün; ürün başına 2 LLM + 1 image çağrısı + ≤3 MCP yazma.

## Constitution Check

*GATE: Phase 0'dan önce geçmeli; Phase 1 sonrası yeniden kontrol edilir.*

- **I. Bounded Context İzolasyonu** ✅ Agent hiçbir servisin DB'sine dokunmaz; tüm
  okuma/yazma Catalog ve File'ın MCP sözleşmeleri üzerinden. File görseli kendi BC'sinde
  sahiplenir. Yeni paylaşılan domain modeli yok.
- **II. Zengin Aggregate** ✅ Tamlık kuralı Product aggregate'inde korunur
  (`RecalculateCompleteness`); yeni `SetDescriptionIfEmpty`/`SetImageUrlIfEmpty`
  metotları idempotent-yazmayı aggregate içinde tutar. File yeni aggregate **kazanmaz** —
  görsel dosya-sistemine yazılan bir storage utility; bilinçli sapma (audit gerekirse revisit).
- **III. Vertical Slice + CQRS, Repository yok** ✅ Catalog'a yeni Command/Agent
  slice'ları (`IDocumentSession`, `[Transactional]`); File'a upload command slice'ı + MCP
  tool (diske yazar, DB yok). MCP tool'ları ince sarmalayıcı.
- **IV. Result Pattern** ✅ Yeni handler/aggregate metotları `FeatureResultModel` /
  `ResultDomain` döner; "zaten dolu → atlandı" bir Result kodu ile taşınır (yeni
  resource sabiti).
- **V. Scope-Tabanlı Yetki** ✅ Yeni `file.write` scope; Catalog yazma `catalog.write`.
  Agent kendi `enrichment.agent` client_credentials kimliğiyle token alır. Rol yok.
- **Teknoloji kısıtları** ✅ Paketler Directory.Packages.props'a; DI Scrutor marker'ları;
  GlobalUsings; Aspire AppHost'ta resource; agent tipleri Singleton.

**Gate: PASS** — anayasa ihlali yok; Complexity Tracking gerekmez.

## Project Structure

### Documentation (this feature)

```text
specs/002-product-enrichment-agent/
├── spec.md
├── plan.md              # bu dosya
├── research.md          # Phase 0 — çözülen kararlar (D1-D3 + ek)
├── data-model.md        # Phase 1 — Product (enriched) + dosya-sistemi görsel saklama
├── contracts/
│   └── mcp-tools.md      # Phase 1 — yeni MCP tool sözleşmeleri
├── quickstart.md        # Phase 1 — uçtan uca doğrulama
└── tasks.md             # /speckit-tasks çıktısı (bu komut üretmez)
```

### Source Code (repository root)

```text
src/agents/ProductEnrichmentAgent/          # YENİ WebApi + BackgroundService
├── Program.cs                              # OpenAI chat+image client, 2 agent, workflow, MCP client, worker
├── EnrichmentBackgroundService.cs          # eksik ürünleri çeker, ürün başına workflow çalıştırır
├── EnrichmentWorkflow.cs                   # sıralı 2 agent (Description → Image) + yazma adımları
├── Agents/DescriptionAgent.cs              # ad+marka → açıklama (ChatClientAgent + prompt)
├── Agents/ImageAgent.cs                    # ad → image prompt → OpenAI image API tool → bytes
├── ClientCredentialsTokenHandler.cs        # MCP çağrılarına m2m token ekler (forward değil)
├── ConstValues.cs                          # (varsa) agent'a özel sabitler
└── ProductEnrichmentAgent.csproj

src/services/catalog/Catalog.Api/Domains/Products/
├── Product.cs                              # + SetDescriptionIfEmpty, SetImageUrlIfEmpty
├── Features/Agent/ListIncompleteProducts.cs    # YENİ query (eksik ürün adayları)
├── Features/Commands/SetProductDescription.cs  # YENİ (boşsa yaz)
├── Features/Commands/SetProductImage.cs        # YENİ (boşsa yaz)
└── ProductMcpTools.cs                      # + list_incomplete_products, set_product_description, set_product_image

src/services/file/File.Api/
├── Domains/Images/Features/Commands/UploadImage.cs  # bytes → Images/{ProductId}.png (DB yok)
├── Domains/Images/ImageMcpTools.cs         # upload_product_image (idempotent: dosya varsa atla)
├── Program.cs                              # + MapMcp("/mcp"), UseStaticFiles(Images), file.write scope
└── File.Api.csproj                         # Images/ klasörü (mevcut)

src/others/Identity.Server/Config.cs        # + file.write scope, file.api resource scope, enrichment.agent client
src/others/Common/Utils/Constants/AuthorizationScopes.cs  # + FileWrite
src/aspire/AppHost/AppHost.cs               # ProductEnrichmentAgent resource + referanslar
src/services/gateway/...                    # /file (images statik) + /mcp/file route (gerekirse)
Directory.Packages.props                    # Agent Framework Workflows + OpenAI image paket sürümleri
```

**Structure Decision**: Yeni bağımsız agent projesi `src/agents/ProductEnrichmentAgent`
(ChatAgent gibi WebApi, ama kullanıcıya-dönük uç yerine bir BackgroundService barındırır).
Enrichment agent'ı kendi MCP sabitlerini kendi `ConstValues`'ında tutar (ChatAgent'la ortak
tool yok). Catalog ve File kendi vertical slice'larını
kazanır; hiçbir servis diğerinin şemasına dokunmaz. File.Api görseli **dosya-sistemine**
yazar (yeni DB/aggregate yok); mevcut legacy Marten/`fileDb` sökümü ayrı bir cleanup'tır.

## Complexity Tracking

> Constitution Check PASS — doldurulacak ihlal yok.