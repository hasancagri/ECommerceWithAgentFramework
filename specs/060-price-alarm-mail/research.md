# Research: Fiyat Alarmı + Mail Bildirimi (060)

Tüm kararlar kod keşfiyle doğrulandı; NEEDS CLARIFICATION kalmadı.

## R1 — Eski fiyat kaynağı: `ProductChangedEvent`'e additive `OldPrice`

- **Decision**: `Shared/IntegrationEvents.cs` `ProductChangedEvent`'e `decimal? OldPrice = null` eklenir; Catalog `UpdateProduct` handler'ı fiyat değiştiğinde doldurur (`UpdateProduct.cs:120-125` zaten `oldPrice` hesaplıyor).
- **Rationale**: Mail eski+yeni fiyat ister (FR-005); event bugün yalnız yeni `Price` taşır. Additive default'lu alan konvansiyonu eski tüketiciyi (Storefront) kırmaz.
- **Alternatives**: (a) Kitaplık BC her ürünün son fiyatını kendinde tutar — ekstra state + drift riski + tüm ürün akışını dinleme yükü; (b) tetik anında Catalog'a REST sorgusu — senkron bağımlılık. İkisi de red.

## R2 — Tetik tanımı: her fiyat değişimi, alarm yaşar

- **Decision**: Tetik = `OldPrice.HasValue && NewPrice != OldPrice` (yön bakılmaz). Alarm tetikte KAPANMAZ; kullanıcı kaldırana dek her değişimde mail (2026-09-02 kullanıcı kararı — "kurallarla uğraşmak istemiyorum", tek-atımlık terk edildi).
- **Rationale**: FR-003/FR-004 (güncel). `OldPrice == null` (fiyat-dışı değişiklik) tetiklemez. Gürültü kontrolü kullanıcıda: rahatsız olan alarmı kaldırır.

## R3 — Kullanıcı e-postası: alarm kuruluşunda snapshot

- **Decision**: WebApp, cookie claim'inden (`email`) adresi alarm kurma komutuna koyar; `PriceAlarm` aggregate'i e-postayı snapshot olarak taşır; `PriceAlarmTriggered` event'i e-postayı içerir.
- **Rationale**: Email claim access token'da YOK (yalnız id_token/userinfo — `UserInfoEndpoint.cs:30-34`); worker'ın Identity'ye sorması BC izolasyonunu deler. Snapshot = worker MCP/REST turu olmadan mail atar.
- **Alternatives**: (a) access token'a email claim eklemek — her API çağrısına PII taşır, red; (b) worker userinfo çağırır — izolasyon + makine-kimliği sorunu, red.

## R4 — Yeni "kitaplık" BC: `Library.Api`

- **Decision**: `src/services/library/Library.Api`, DB `libraryDb`, şema `library`. İlk aggregate `PriceAlarm`; bildirim izi (`NotificationRecord`) da bu BC'de. Favori/listeler İLERİDE aynı BC'ye.
- **Rationale**: Alarm = kullanıcının ürünle kalıcı ilgi beyanı; Customer bilinçli izole/event'siz ödeme-kimliği dilimi (cüzdan+adres), Catalog ürün gerçeği — ikisi de yanlış ev. Kullanıcı oturumunda netleşti (2026-09-02): Customer=ödeme destek verisi, Library=ilgi/ilişki kayıtları, telemetri ayrı.
- **Alternatives**: Customer'a gömme (event'siz karakteri bozar), Catalog'a gömme (kullanıcı verisi sızar), worker'da tutma (DB'siz worker BC'leşir). Red.

## R5 — Worker: MAF Workflows (`NotificationAgent`)

- **Decision**: `src/agents/NotificationAgent` (ad kullanıcı kararı, ChatAgent'la tutarlı; Aspire adı `notification-agent`) — DB'siz, Reviews.Moderation şablonu (Wolverine handler + `RetryWithCooldown(10s,30s,60s).Then.MoveToErrorQueue()`). İçeride MAF **Workflows** boru hattı: Enrich → Decide → Compose → Send → Outcome; handler `NotificationSent` cascade-return eder.
- **Rationale**: Kullanıcı öğrenme hedefi = WorkflowContext/Executor. `Microsoft.Agents.AI.Workflows` 1.13.0 props'ta tanımlı, repo'da İLK kullanım. 1.13.0'da `ReflectingExecutor` obsolete → `Executor` + `ConfigureProtocol` (memory: agent-framework-workflows-executor-api).
- **Hata yolu (kullanıcı kararı)**: Mail gönderiminde hata → worker `NotificationException` FIRLATIR → Wolverine retry (10s/30s/60s) → tükenirse error queue. Compose LLM hatası exception DEĞİL (yedek şablonla devam — spec Assumption); yalnız gönderim hatası retry'a gider.

## R6 — Compose: LLM structured output + yedek şablon

- **Decision**: Compose executor'ı ChatClientAgent ile `MailDraft(Subject, BodyHtml)` structured JSON üretir (Türkçe, hitap + ürün adı + eski/yeni fiyat + link). LLM hatasında sabit şablonla devam edilir (mail hiç gitmemesinden iyidir — spec Assumption).
- **Rationale**: ModerationAgent deseni (structured record); kişiselleştirme spec varsayımı.

## R7 — Send: Mail.Mcp + minik agent (MCP-yalnız-agent kuralı)

- **Decision**: Yeni standalone MCP server `src/agents/Mail.Mcp`: tek tool `send_mail(to, subject, bodyHtml)` — MailKit ile SMTP'ye (Mailpit) gönderir. Send executor'ındaki minik ChatClientAgent bu tool'u LLM tool-seçimiyle çağırır; imperatif `CallToolAsync` YOK. ChatAgent'a Mail.Mcp KAYDEDİLMEZ (mail müşteri chat yüzeyine ait değil).
- **Rationale**: Anayasa v1.8.1 "MCP'yi yalnız agent tüketir"; repo'da mevcut mail kodu/SMTP paketi YOK (arandı) → MailKit `Directory.Packages.props`'a eklenir. Repo'da ilk standalone MCP host (mevcutlar API-içi `/mcp`).
- **Alternatives**: Worker'dan doğrudan SMTP — MCP öğrenme hedefini ıskalar; tool'u imperatif çağırmak — anayasa ihlali. Red.

## R8 — Mailpit: ham Aspire container

- **Decision**: AppHost'a `builder.AddContainer("mailpit", "axllent/mailpit")` + endpoint'ler (SMTP 1025, HTTP UI 8025). Mail.Mcp SMTP host/port'u endpoint referansından env ile alır (Options pattern `SmtpOptions`).
- **Rationale**: FR-009 (yerel posta görüntüleyici). `CommunityToolkit.Aspire.Hosting.Mailpit` paketi yerine ham container — ekstra bağımlılık yok, sürüm uyumluluk riski yok.

## R9 — Bildirim izi: `NotificationSent` event'ini Library BC tüketir

- **Decision**: Worker `NotificationSent(UserId, ProductId, Email, Success, Detail)` yayınlar; Library.Api tüketip `NotificationRecord` dokümanı yazar. E-posta boşsa Decide adımı gönderimi atlar, `Success=false, Detail="no-email"` izi yine düşer.
- **Rationale**: FR-007/SC-005 kalıcı iz ister; fanout'a bağlı kuyruk yoksa mesaj kaybolur. İz kitaplık alanının verisi — ileriki "Bildirimlerim" ekranının tohumu.

## R10 — Kapsam dışı bırakılan altyapı

- **Decision**: Gateway route YOK (WebApp servise doğrudan service discovery ile gider; library dış tüketiciye açılmıyor). Library.Api'de MCP endpoint/tool YOK (JIT — agent tüketicisi yok). ChatAgent'a dokunuş YOK.
- **Rationale**: "Yapı hazır, doldurmak ihtiyaç güdümlü" konvansiyonu.

## Bilinen tuzaklar (implement'te dikkat)

- Yeni projelere `Properties/launchSettings.json` ŞART (yoksa Production açılır, 500) — memory: aspire-service-needs-launchsettings.
- Wolverine `*EventHandlers` keşfi atlayabilir → `opts.Discovery.IncludeType<...>()` — memory: wolverine-eventhandler-includetype.
- MCP tool optional param'a default ŞART (nullable yetmez) — `send_mail`'de tüm param'lar zorunlu tutulacak.
- Binding'i TÜKETİCİ kurar (soğuk-açılış dersi): library `product.changed`'e, worker `library.price-alarm-triggered`'a, library `notifications.sent`'e kendi kuyruğunu kendisi bağlar.