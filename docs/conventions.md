# Mimari Konvansiyonlar (taşınabilir katman)

Bu dosya proje-bağımsızdır: DDD/VSA mimarisi + kod disiplini. Yeni projeye AYNEN kopyalanabilir.
Proje-özel bilgi (komutlar, servis listesi, feature'lar) `CLAUDE.md`'dedir. **İLKE N** = projenin
`.specify/memory/constitution.md` ilkesi (her projenin kendi anayasası olur).

## Spec-Driven Development (spec-kit)

Önemsiz olmayan her feature spec-kit akışıyla (`.claude/skills/speckit-*`): `constitution → specify
→ clarify(ops) → plan → tasks → implement`. Koda atlamadan önce en az spec (gerekirse plan) üretilir.

- **Anayasa (`.specify/memory/constitution.md`) her şeyin üstünde.** Bu konvansiyonlar "nasıl uygulanır",
  anayasa "ne pazarlık edilemez" — çakışırsa **anayasa kazanır**. İlkeye İLKE N ile atıf yapılır;
  ilkenin kendisini tekrar etme, anayasadan oku.
- **Artefakt ölçekleme:** _trivial_ = spec-kit'siz; _küçük_ (tek aggregate, yeni tablo/kontrat/event
  yok) = yalnız `spec.md`+`tasks.md`; _tam_ (yeni aggregate/tablo/event/kontrat) = tam akış. Şüphede üst kademe.
- **Domain-TDD (İLKE VI):** saf domain (aggregate davranışı, saga `On*`, VO) test-first; test task'ı
  implementasyondan önce. Handler/endpoint/UI/altyapı bu kuralın dışı (test-sonra/canlı doğrulama).

## Domain süreci belgesi — FLOW.md (İLKE VII uygulaması)

Anayasa "her BC'nin domain süreci belgelenir" der; **nasıl**'ı budur:

- **Dosya:** her BC kökünde tek `FLOW.md` (ör. `src/services/<bc>/FLOW.md`). Kod-yakını → az bayatlar.
- **Altitude:** domain-önce, ubiquitous dille — "hangi iş adımı, hangi sırayla, hangi olayı doğurur".
  Teknoloji/class **dökümü değil**; class adı yalnız satır sonunda `(Aggregate.Metot → Event)` **kenar-
  anchor** (koda atlama). Satır numarası YOK (bayatlar).
- **İçerik:** (1) BC ne yapar tek cümle, (2) sıralı **Süreç** adımları, (3) **Domain kuralları**
  (süreci yöneten değişmezler), (4) **Sınır** (BC'nin dokunmadığı). ~1 ekran. Örnek: `procurement/FLOW.md`.
- **Güncelleme tetiği (dar):** yalnız domain süreci değişince (yeni/silinen command-event-policy, adım
  sırası). Mekanik rename/refactor tetiklemez. Feature süreci değiştiriyorsa FLOW.md **aynı PR'da** güncellenir.
- **Guard:** `scripts/check-flow-links.sh` — FLOW.md'deki kenar-anchor tip adlarının kod tabanında hâlâ
  VAR olduğunu doğrular (rename/silme driftini yakalar). Sıra driftini yakalamaz — o review + tetik disiplini.
- **Non-.NET BC (ör. Python/Personalization):** aynı kural; anchor o dilin sınıf/fonksiyon adıdır.

## Mimari kurallar (anayasa-atıflı)

Her rol için: anayasa İLKE = "ne"; buradaki satır = "nasıl uygulanır" (koda özgü, anayasada olmayan).

- **BC izolasyonu (İLKE I).** Her servis = 1 BC = kendi Postgres DB'si + Marten şeması; DB paylaşımı YOK.
  Başka BC'nin aggregate/tablo/DbContext'ine erişim yasak; context'ler arası tek kanal: integration
  event + MCP + sanksiyonlu gRPC.
- **Aynı kavram farklı BC'de farklı model (İLKE I).** "Ürün" bir BC'de zengin aggregate, ötekinde sepet
  entity'si, üçüncüde read-model satırı olabilir — birinin modelini ötekine sızdırma.
- **Zengin aggregate (İLKE II).** Ortak `AggregateRoot`'tan türer (Id + denetim alanları); anemik yasak.
  Entity base ALMAZ. VO = `record` + private ctor + statik `Create`.
- **Invariant aggregate içinde (İLKE II).** Koleksiyon private, okuma `IReadOnlyList`, mutasyon yalnız
  aggregate metodundan. **Yeni kural → önce aggregate metoduna bak, handler'a değil.**
- **VSA + CQRS (İLKE III).** Kod teknik katmana değil domain feature'ına göre (yapı aşağıda). Command
  (`[Transactional]`, `IDocumentSession` yazar) / Query (yalnız okur) ayrı slice. **Repository YOK** —
  handler doğrudan `IDocumentSession`, slice-arası çağrı `IMessageBus`. Endpoint = Minimal API.
- **Result pattern (İLKE IV).** Beklenen hata = Result (exception değil); detay Kod standartlarında.
- **Scope yetki (İLKE V).** Servisler talep ettikleri scope'larla korunur; **rol = scope demeti**,
  downstream rolü görmez. Kurulum detayı proje CLAUDE.md'de.

### VSA dosya yapısı

```
Domains/<Aggregate>/
  <Aggregate>.cs                  # zengin aggregate root (private setter, factory + davranış)
  <Aggregate>EndpointExtension.cs # feature endpoint'lerini gruplar + map'ler
  <Aggregate>McpTools.cs          # bu aggregate için MCP tool sarmalayıcıları
  Features/
    Commands/<Name>.cs            # yazma slice'ları
    Queries/<Name>.cs             # okuma slice'ları
    Agents/<Name>ForAgent.cs      # agent'a açık slice (klasör ÇOĞUL; MCP expose eder)
```

- **Bir feature = bir static class**: `record` command/query + `Response` + `Handler` (düz sınıf,
  `Handle` metodu) + endpoint-extension. Command mı query mi ayır, doğru klasöre koy.
- **Yapı hazır, doldurmak ihtiyaç güdümlü (JIT).** `*EndpointExtension` + `Features/` iskeleti nereye
  ne konacağını gösterir; ama her aggregate metodu için endpoint ÜRETME zorunluluğu YOK. Endpoint =
  gerçek tüketici (UI/agent/dış) çağırınca açılır; kullanılmayan endpoint silinebilir. İskelet kalır.
- Endpoint: `CurrentUser.Load(httpContext.User)` ile kullanıcı, `IMessageBus.InvokeAsync` ile handler,
  `.RequireAuthorization(...)` ile koruma; `IsSuccess ? Ok : BadRequest`.
- API sürümleme URL-segment (`v1`); doküman Scalar ile kök.

## Kod standartları

- **Result — handler.** `FeatureObjectResultModel<T>`/`FeatureResultModel`/`FeatureListResultModel`/
  `FeaturePagedResultModel` döner (`Common.Results`, `new` YOK — statik fabrika `Ok`/`Error`/`NotFound`).
  Hata `MessageItem.Code` = resource sabiti (serbest metin değil).
- **Result — aggregate.** Davranış/fabrika metotları `ResultDomain`/`ResultDomain<T>` döner (void mutator
  dahil). Çağıran: `var r = agg.Method(...); if(!r.IsSuccess) return <err>(r.Messages);`. **Muaf:** saf getter.
- **Hata kodu sahipliği.** Her servis kendi kodlarına sahip: `<Service>/Constants/<Service>ResourceConstants.cs`
  (generic/domain ayrımı yapılmaz). Framework-içi kodlar servise sızmaz.
- **Aggregate klasör.** `Domains/<X>/` hemen altı tek `: AggregateRoot`; iç içe aggregate yok. İstisna:
  domain-service/seeder/read-model (aggregate değil) aynı BC'de ayrı yerleşebilir.
- **VO tek dosya.** Aggregate'in TÜM VO'ları `<Aggregate>/ValueObjects/<Aggregate>ValueObjects.cs`'te.
- **Enum aggregate dosyasında** (`OrderStatus` → `Order.cs`); ayrı dosya/`Enumeration` base yok.
- **Aggregate davranışını private helper'a parçalama** — davranış mantığı inline (bilinçli tekrar).
  İSTİSNA: guard/invariant kontrolü helper'a çıkarılabilir (aynı guard'ın 3. kopyası tutarsızlık üretir). **VO muaf.**
- **Aggregate metodu yalnız handler'dan çağrılır** — başka aggregate metodundan (factory dahil) değil. **VO muaf.**
- **Aggregate public metodu:** `/// <summary>` metodun ne yaptığını yazar (handler-listesi remarks kuralı YOK).

## Konvansiyonlar

- **Madde ≤300 karakter.** Tüm repo dokümanları (spec/tasks/CLAUDE.md/constitution). Aşan maddeyi böl
  veya ayrıntıyı ilgili yere taşı; tasks.md ne yapılacağını listeler, nasılını değil.
- **GlobalUsings:** her projede tek `GlobalUsings.cs`; paylaşılan namespace oraya, dosyaya `using` serpme.
- **`Domains/` yalnız domain:** teknik sabit (resource kodu) `<Service>/Constants/`'e, `Domains/`'e değil.
- **DI = Scrutor otomatik:** `ITransientDependency`/`IScopedDependency`/`ISingletonDependency` marker'ı
  implemente et; `AddAllDependencies()` kaydeder. `Program.cs`'te elle kayıt yapma.
- **Agent tipleri Singleton** — framework başlangıçta yakalar; per-user davranış = token'ı çağrı anında enjekte.
- **Config = Options pattern (tip'li).** `IConfiguration`'dan DOĞRUDAN okuma YASAK (`config["A:B"]`,
  `GetValue<T>`, `Get<T>()` dahil). Her section → `Options/` POCO'su; tüketici düz `T` enjekte eder (`IOptions<T>` değil).
- **Options bağlama:** `AddOptions<T>().BindConfiguration(nameof(T)).ValidateDataAnnotations().ValidateOnStart()`.
  **İstisna:** service-discovery + dinamik-key lookup (statik section değil).

## Servisler-arası desenler

- **Integration event.** Kontrat paylaşılan bir sözleşme kitaplığında; Wolverine→RabbitMQ **fanout**.
  Yayıncı exchange deklare eder, **binding'i TÜKETİCİ kurar** (soğuk-açılış kayıp dersi). Additive alan
  default'lu ekle (eski tüketici kırılmaz).
- **Sanksiyonlu gRPC (İLKE I).** Yalnız anlık-tutarlılık akışı; çağıran karşının API'sine erişir
  (DB'sine değil). Sunucu ince sarmalayıcı (iş mantığı yok, `IMessageBus`'a devreder).
- **MCP yalnız agent tüketir.** Agent olmayan kod (WebApp/servis) imperatif `CallToolAsync` süremez →
  REST/gRPC. Chat akışında MCP DOLAYLI: agent tool'u LLM prompt'uyla seçer, elle `CallToolAsync` YOK.
  MCP tool YALNIZ `Features/Agents/<X>ForAgent` slice'ını çağırır (ince sarmalayıcı).
- **Cache (AOP decorator).** Query'ye `[Cached("tag", ttl)]`, command'a `[InvalidatesCache("tag")]` —
  `IMessageBus` decorator'ı. Yalnız BC'nin KENDİ verisi + herkese-aynı + bayat-toleranslı;
  yazma-yolunu besleyen query cache'LENMEZ.

## Bilinçli tekrar (tek gerekçe)

Bazı yerlerde kod/veri BİLEREK tekrarlanır — kırılgan bağımlılık yerine BC izolasyonu tercih edilir.
Aşağıdakilerin hepsi bu kurala dayanır, ayrı ayrı gerekçelendirilmez:

- Aggregate'te davranış private helper'a parçalanmaz (davranış inline; guard helper'ı istisna).
- Agent slice `Features/Commands|Queries`'e gitmez (kendi handler'ını taşır, `IMessageBus` ile bile değil).
- Paylaşılan seed verisi (taksonomi/registry) İKİ BC'de ayrı seed edilir — sözleşme AD'dır.
- Generic hata kodu servisler arası tekrarlanır.
