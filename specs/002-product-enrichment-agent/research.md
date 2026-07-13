# Phase 0 Research: Product Enrichment Agent

Spec'in bıraktığı D1-D3 kararları + tasarımdan doğan ek kararlar burada çözülür.
Her karar: seçim, gerekçe, elenen alternatifler.

## D1 — Tetikleme mekanizması

**Karar**: Yeni agent projesi içinde bir **BackgroundService** (IHostedService)
başlangıçta eksik ürünleri çeker ve toplu işler.

**Gerekçe**: US2 (toplu) çekirdek iş problemidir; 30 seed ürünü tek koşuda satışa
hazırlar (SC-002). Ayrı bir tetik ucu/altyapı gerektirmez, en basiti.

**Elenen**: Event-driven (RabbitMQ, ürün-eksik-oluşturulunca) — süregen akış için
zarif ama seed toplu senaryosuna değer katmaz; ileride eklenebilir. Manuel MCP/HTTP
uç — worker deseni zaten operatör tetiğini kapsar, ekstra uç gereksiz.

## D2 — Agent yerleşimi

**Karar**: Yeni bağımsız WebApi projesi `src/agents/ProductEnrichmentAgent`.

**Gerekçe**: Enrichment headless bir batch worker; ChatAgent ise per-user OpenAI-uyumlu
sohbet uçları sunar. İkisini karıştırmak yaşam-döngüsü ve sorumluluk açısından kirli.
Ayrı proje temiz sınır sağlar ve agent tiplerini Singleton tutma kuralına uyar.

**Elenen**: ChatAgent içine 3. agent — sohbet ile headless batch'i aynı projede
karıştırır, endpoint yüzeyini kirletir.

## D3 — Üretilen görselin saklanması

**Karar**: File.Api görseli **MCP upload tool'u** ile alır, **yalnız dosya sistemine**
(`Images/{ProductId}.png`) yazar ve statik serve eder; deterministik servable URL döner.
Yeni aggregate/DB **yok** — idempotency dosya varlık kontrolüyle. Catalog `ImageUrl`'e yazılır.

**Gerekçe**: Bounded context I — dosya saklama File'ın işidir; agent yalnız File'ın
sözleşmesini çağırır. Kullanıcı seçimi upload transportu için MCP. `Images/` klasörü
zaten eklenmiş. Statik serve, URL'in gerçekten görüntülenebilir olmasını sağlar (SC-003).

**Elenen**: Agent'ın doğrudan lokal/statik saklaması — File BC'sini atlar, sınırı
zayıflatır. RabbitMQ ile byte taşıma — ~1MB görsel için ağır; mevcut course-picture
event plumbing'i bu feature'da kullanılmaz (temizlik notu: legacy, kaldırılabilir).

## Ek-1 — Görsel üretim yeteneği

**Karar**: Image agent (ChatClientAgent) üründen bir **görsel prompt'u** üretir; asıl
görseli OpenAI **image API** (`gpt-image-1`, `quality: low`) üretir (b64 PNG, 1024×1024;
gpt-image-1'in tabanı). File.Api saklarken **256×256'ya küçültür** (küçük storage/hız).

**Gerekçe**: Agent Framework agent'ı metin/tool-call döndürür, ham byte değil. Prompt'u
LLM'e kurdurup görseli image API'ye ürettirmek doğru ayrımdır. OpenAI hattı zaten mevcut
(OpenAIClient); `GetImageClient` ile aynı SDK. `gpt-image-1` gerçek, placeholder-olmayan
görsel üretir (SC-003).

**Elenen**: DALL·E 3 — uygun ama gpt-image-1 daha güncel/kaliteli. Stable Diffusion vb.
harici sağlayıcı — yeni bağımlılık, gereksiz.

## Ek-2 — Agent kimliği ve yetki

**Karar**: Yeni `enrichment.agent` client_credentials client'ı (Identity.Server);
scope'lar `catalog.read`, `catalog.write`, `file.write`. Worker açılışta token alır,
MCP çağrılarına `ClientCredentialsTokenHandler` ekler.

**Gerekçe**: BackgroundService'in gelen HTTP isteği yoktur; ChatAgent'ın
TokenInjectingHandler "forward" deseni burada işlemez. Worker kendi m2m token'ını
almalı. `m2m.client` deseni örnek; ayrı client, en-az-yetki (only needed scopes).

**Sonuç (yeni scope)**: `file.write` scope + File.Api'nin MCP yüzeyini koruma. Config.cs'te
`file.api` resource'una scope eklenir (şu an "HTTP yok → scope yok" notu güncellenir).

## Ek-3 — Agent Framework Workflow şekli

**Karar**: Ürün başına **sıralı** workflow: DescriptionExecutor → ImageExecutor. Her
executor kendi alanını üretir ve **bağımsız** olarak Catalog'a yazar (kısmi başarı:
biri başarısızsa diğeri yine yazılır; ürün ancak iki alan da dolunca satışa çıkar).

**Gerekçe**: Kullanıcı sıralı istedi; FR-006 alan-bağımsız yazmayı gerektirir. Marten
`RecalculateCompleteness` iki alan dolunca `IsComplete` yapar; workflow sıralamadan
bağımsız doğru sonuç verir.

**Elenen**: Paralel fan-out/fan-in — mümkün ama istenen sıralı; ekstra karmaşıklık yok.

## Açık teyit gerektirenler (implement anında)

- `Microsoft.Agents.AI.Workflows` paket adı/sürümü ve `WorkflowBuilder`/executor API'si
  implement başında doğrulanır (Directory.Packages.props'a eklenir).
- OpenAI image client'ın Microsoft.Extensions.AI köprüsü mü yoksa doğrudan OpenAI SDK mı
  kullanılacağı; doğrudan `OpenAIClient.GetImageClient(...)` varsayılan.