# Research: ChatAgent Kalıcı Konuşma Memory'si

Tüm kararlar 2026-07-24 brainstorming'inde ve paket yüzeyi incelemesinde netleşti; NEEDS
CLARIFICATION kalmadı.

## PİVOT (implement sırasında, 2026-07-24): R1/R2 mekanizması değişti

Derleme anında görüldü: `IConversationStorage`/`IAgentConversationIndex`/`IResponsesService` MAF
hosting'de **internal** — 1.11.1 ve en güncel 1.15.0-alpha dahil. Takas (R2) imkânsız; R8 riski
gerçekleşti. Davranış (spec) değişmedi; mekanizma public seam'e taşındı:

- ChatAgent'a kendi `POST /v1/chat` SSE ucu eklendi: geçmiş `ConversationStore`'dan (Marten)
  pencereyle yüklenir → `agent.CreateSessionAsync` + `SetInMemoryChatHistory` → `RunStreamingAsync`
  → koşu sonunda kullanıcı+asistan+araç mesajları depoya yazılır.
- Framework'ün hosted Responses/Conversations uçları widget yolundan çıktı (map'li kalanlar
  kullanılmıyor); `previous_response_id` akışı yine öldü.
- Mesajlar `ChatMessage` olarak STJ + `AIJsonUtilities.DefaultOptions` ile saklanır.
- Kazanç: sıfır internal-API bağımlılığı; R8 yüzeyi kendi kodumuza indi.

## R1 — Mekanizma: Conversations API (previous_response_id zinciri değil)

- **Decision**: Çok-turlu geçmiş MAF Conversations API ile taşınır; Responses çağrısı
  `CreateResponse.Conversation` referansı alır. WebApp `previous_response_id`'yi bırakır.
- **Rationale**: Kalıcılık seam'i saf depolama arayüzleri (`IConversationStorage`,
  `IAgentConversationIndex`); liste/aç API'leri hazır; response zinciri depoya bağlanamıyor.
- **Alternatives**: (B) `IResponsesService`'i kalıcılaştırmak — depo değil koca servis arayüzü
  (execute+stream dahil); liste doğal çıkmaz. (C) Geçmişi BFF'te tutmak — her turda token maliyeti,
  WebApp'e yersiz DB. İkisi de reddedildi.

## R2 — Depo: `chatAgentDb` + Marten implementasyonları

- **Decision**: `MartenConversationStorage : IConversationStorage` ve
  `MartenAgentConversationIndex : IAgentConversationIndex`; yeni `chatAgentDb` (AppHost resource),
  `SchemaConstants`'a ChatAgent şeması. Wolverine eklenmez.
- **Rationale**: Stack tutarlılığı (her depolama Marten/Postgres); `AddOpenAIConversations`
  in-memory'yi "yalnız dev/test" diye kaydediyor — üretim yolu değişim bekliyor.
- **Kayıt sırası notu**: In-memory kayıtların TryAdd/Add davranışı implement anında doğrulanır;
  gerekirse `Services.Replace(...)` ile bizimkiler kesin kazanır (T görevi var).
- **Alternatives**: Redis (uçucu/uygunsuz sorgu modeli), EF Core (stack dışı) — reddedildi.

## R3 — Bağlam penceresi: framework yolu kırpar, UI yolu tam okur

- **Decision**: `MartenConversationStorage.ListItemsAsync` (framework'ün model input'u kurduğu yol)
  son N item döner (`Chat:ContextWindowItems`, varsayılan 40). UI, item'ları bizim
  `my-conversations` uçlarından okur — o yol daima eksiksizdir. Depoya kırpma uygulanmaz.
- **Rationale**: Tek depo, iki okuma yolu; FR-004 (UI tam) ile FR-005 (model pencereli) çelişmeden
  sağlanır. Özetleme bilinçli kapsam dışı (YAGNI).

## R4 — Yetki: JWT bearer + kaynak sahipliği; yeni scope yok, rol yok

- **Decision**: ChatAgent JWT bearer doğrulaması kazanır (Authority = Identity.Server).
  `my-conversations` uçları kimlik ister; sahiplik token'daki `sub` (UserId) ile süzülür.
  Anonim conversation yaratma serbesttir (OwnerUserId null); anonim uçlarda listeleme yoktur.
- **Rationale**: Anayasa V'in özü "scope, rol değil"; kişiye-özel veri erişimi scope değil
  kaynak-sahipliği meselesidir. Yeni scope (Identity Config + WebApp login isteği) gereksiz tören.
- **Alternatives**: `ChatRead` scope'u — istemci zaten tek (WebApp BFF); katma değer yok, reddedildi.

## R5 — Conversation yaratma: kendi ucumuzdan, metadata'ya güvenmeden

- **Decision**: Konuşmayı `POST /v1/my-conversations` yaratır; OwnerUserId sunucuda token'dan
  yazılır. Framework'ün `POST /v1/conversations` ucu dışarı açılmaz (map edilmez).
- **Rationale**: İstemcinin gönderdiği metadata'daki kimliğe güvenmek sahiplik modelini deler;
  kimlik daima sunucuda çözülür (agent-auth duruşuyla tutarlı).

## R6 — Anonim TTL: BackgroundService süpürücü

- **Decision**: Saatlik tik'li `AnonymousConversationCleanup`: OwnerUserId null VE son aktivite
  24 saatten eski konuşmaları item'larıyla siler. Config: `Chat:AnonymousTtlHours` (24).
- **Rationale**: Basit, tek instance varsayımıyla yeterli. Hangfire bilinçli eklenmedi —
  o Supplier.Gateway'in öğrenme kurulumu; burada pano/kalıcı zamanlama ihtiyacı yok.

## R7 — WebApp akışı: id yönetimi ve UI

- **Decision**: Widget conversation id'yi `sessionStorage`'da tutar (login + anonim aynı mekanik).
  BFF uçları: `POST /chat/conversations` (yarat), `GET /chat/conversations` (liste, login),
  `GET /chat/conversations/{id}` (item'lar, login+sahiplik), `POST /chat/stream` (id zorunlu).
  Login panelinde sohbet listesi + "yeni sohbet"; anonimde yalnız süreklilik.
- **Rationale**: Cookie yerine sessionStorage: BFF state'siz kalır, "aynı oturum" semantiği
  tarayıcı sekme oturumuyla birebir; login listesi zaten sunucudan gelir.

## R8 — Alpha paket riski

- **Decision**: MAF hosting arayüz implementasyonları `Conversations/` klasöründe izole tutulur;
  sürüm CPM'de sabit. Sürüm atlamasında kırılırsa tek klasör elden geçer.
- **Rationale**: 1.11.1-alpha; `IConversationStorage` yüzeyi değişebilir — bilinen, kabul edilmiş
  MAF riski (bkz. memory: executor API kırılması emsali).