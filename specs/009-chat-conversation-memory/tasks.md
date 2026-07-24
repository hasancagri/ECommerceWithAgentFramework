# Tasks: ChatAgent Kalıcı Konuşma Memory'si

**Input**: Design documents from `/specs/009-chat-conversation-memory/`

**Prerequisites**: plan.md, spec.md, research.md (R1–R8), data-model.md, contracts/, quickstart.md

**Tests**: Anayasa gereği yeni davranış test edilir: ConversationRules birim testleri (yeni test projesi).

**Organization**: Görevler user story bazında; her story quickstart senaryosuyla bağımsız doğrulanır.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel koşabilir (farklı dosya, bekleyen bağımlılık yok)
- **[Story]**: US1 / US2 / US3 (spec.md öncelikleriyle eşleşir)

## Phase 1: Setup

- [X] T001 master'dan `009-chat-conversation-memory` branch'ini aç
- [X] T002 `src/aspire/AppHost/AppHost.cs`: `chatAgentDb` database resource'u ekle; `chat-agent`
      projesine `WithReference` + `WaitFor` bağla
- [X] T003 [P] `SchemaConstants`'a (src/others/Common) ChatAgent şema adını ekle
- [X] T004 [P] `src/agents/ChatAgent/ChatAgent.csproj`: Marten + Marten.Newtonsoft referansları;
      `GlobalUsings.cs`'e Marten using'leri
- [X] T005 `tests/ChatAgent.Tests` projesini oluştur (xUnit+Shouldly, Supplier.Gateway.Tests şablonu);
      `ECommerceWithAgentFramework.slnx`'e ChatAgent.Tests'i ekle

## Phase 2: Foundational (tüm story'ler için ön koşul)

- [X] T006 [P] `Conversations/ConversationDocument.cs`: ConversationDocument + ConversationItemDocument
      (data-model.md alanları)
- [X] T007 [P] `Conversations/ConversationRules.cs`: saf yardımcılar — DeriveTitle (≤60, kelime sınırı,
      boşsa "Yeni sohbet"), pencere seçimi (son N → kronolojik), TTL filtresi (ownersız + eski)
- [X] T008 `Program.cs` (ChatAgent): Marten kaydı — `chatAgentDb` conn-string, ChatAgent şeması,
      Newtonsoft (repo standardı: non-public setter/ctor)
- [X] T009 PİVOT (research'e işlendi): MAF depolama arayüzleri INTERNAL çıktı →
      `Conversations/ConversationStore.cs` (Marten; pencere `Chat:ContextWindowItems`, append, TTL)
- [X] T010 PİVOT: index sınıfı gereksizleşti (AgentName dokümanda); ayrı dosya YOK
- [X] T011 PİVOT: `Conversations/ChatStreamEndpoint.cs` — POST /v1/chat SSE: Marten'dan pencereli
      geçmiş → `SetInMemoryChatHistory` → `RunStreamingAsync` → done'dan önce kalıcılaştır
- [X] T012 `Program.cs`: JWT bearer doğrulama (Authority=Identity.Server); Common extension DEĞİL —
      audience doğrulaması kapalı (token'da chat audience'ı yok, R4 "yeni scope yok"); anonim uçlar aynı

**Checkpoint**: Uygulama açılır; conversation yaratma/okuma Marten'a gider (in-memory devre dışı).

## Phase 3: US1 — Sohbet kalıcıdır, kopmaz (P1) 🎯 MVP

**Goal**: Konuşma DB'de yaşar; restart bağlamı koparmaz; akış conversation id ile döner.

**Independent Test**: quickstart S1 — mesajlaş, restart et, aynı id ile devam; item'lar DB'de.

- [X] T013 [P] [US1] `tests/ChatAgent.Tests/ConversationRulesTests.cs`: başlık türetme + pencere
      seçimi + TTL filtresi birim testleri
- [X] T014 [US1] `Conversations/MyConversationsEndpoints.cs`: `POST /v1/my-conversations`
      (AllowAnonymous; login'de OwnerUserId=sub, anonimde null; contracts'taki gövde/dönüş)
- [X] T015 [US1] `Program.cs`: MyConversations uçlarını map et; framework'ün
      `MapOpenAIConversations()` çağrısını kaldır (R5 — /v1/conversations dışarı açılmaz)
- [X] T016 [US1] `src/ui/WebApp/Chat/ChatEndpoints.cs`: `POST /chat/conversations` vekili;
      `/chat/stream` gövdesi `{message, conversationId}` olur, Responses çağrısı `conversation` taşır
- [X] T017 [US1] `/chat/stream`: ChatAgent 404'ünde yeni conversation açıp isteği tekrarla;
      yeni id'yi `X-Conversation-Id` header'ıyla bildir (FR-010)
- [X] T018 [US1] WebApp chat widget'ı: `previous_response_id` yerine `sessionStorage`'da
      `chat.conversationId`; ilk mesajda create → stream
- [X] T019 [US1] Derle + birim testler; canlı doğrulama quickstart S1 (restart) ve S6 (depo hatası
      açık hata — sessiz fallback yok)

**Checkpoint**: US1 tek başına teslim edilebilir — kalıcı sohbet, liste olmadan da değerli.

## Phase 4: US2 — Geçmiş konuşmalar listesi (P2)

**Goal**: Login kullanıcı konuşmalarını listeler, tümünü görüntüler, devam eder, yenisini açar.

**Independent Test**: quickstart S2 (liste+tam görüntüleme) ve S3 (sahiplik izolasyonu).

- [X] T020 [US2] `MyConversationsEndpoints.cs`: `GET /v1/my-conversations` (sayfalı, lastActivity desc)
      + `GET /v1/my-conversations/{id}/items` (TAM geçmiş; sahip değilse 404 — varlık sızdırmaz)
- [X] T021 [US2] `ChatEndpoints.cs`: `GET /chat/conversations` ve `GET /chat/conversations/{id}`
      vekilleri (yalnız login; token forward)
- [X] T022 [US2] Widget: sohbet listesi paneli + geçmişi açma (tam mesajlar, araç çağrıları dahil)
      + "yeni sohbet" butonu (id'yi sıfırlar)
- [X] T023 [US2] Canlı doğrulama: API katmanı ✓ (401'ler, sahiplik tek kapıda); S2/S3 tarayıcı turu
      (liste UI + iki kullanıcı) kullanıcı doğrulamasına bırakıldı — spec Canlı Doğrulama notu

## Phase 5: US3 — Anonim oturum sürekliliği + TTL (P3)

**Goal**: Anonim bağlam aynı oturumda sürer; sahipsiz konuşmalar 24s aktivitesizlikte silinir.

**Independent Test**: quickstart S4.

- [X] T024 [US3] `Conversations/AnonymousConversationCleanup.cs`: saatlik BackgroundService —
      OwnerUserId null + LastActivity < now-`Chat:AnonymousTtlHours`(24) olanları item'larıyla sil
- [X] T025 [US3] `appsettings.json` (ChatAgent): `Chat:ContextWindowItems=40`,
      `Chat:AnonymousTtlHours=24`; Program.cs'te cleanup kaydı
- [X] T026 [US3] Canlı doğrulama S4 ✓: 30 saat yaşlandırılan sahipsiz konuşma açılış tikinde silindi,
      taze olan korundu; anonim süreklilik aynı id ile canlıda sürdü

## Final Phase: Polish

- [X] T027 [P] S5: pencere mantığı birim testli (TakeContextWindow); canlı pencere-küçültme demosu
      tarayıcı turuna bırakıldı — spec Canlı Doğrulama notu
- [X] T028 [P] README: ChatAgent bölümüne kalıcı konuşma notu (chatAgentDb, pencere, anonim TTL)
- [X] T029 Tüm çözüm `dotnet build` + `dotnet test`; SC-001..006'yı spec'e işaretle
- [X] T030 Obsidian: chat-history borç notunu kapat/güncelle; memory `chat-history-storage` güncelle

## Dependencies

- Phase 1 → Phase 2 → US1 → US2 → US3 (US2 uçları US1'in create+auth zeminine, US3 cleanup depoya bağlı).
- US2 ile US3 teknik olarak bağımsızdır; US1 sonrası paralel ele alınabilir.

## Parallel Example

- T003/T004 paralel; T006/T007/T010 paralel; T013 implementasyonla paralel; T027/T028 paralel.

## Implementation Strategy

- MVP = US1 (T001–T019): kalıcılık + kopmayan sohbet; liste olmadan da teslim edilebilir değer.
- Sonra US2 (görünür değer: liste), en son US3 (anonim hijyen). Her story sonunda quickstart koşulur.
- Alpha paket sürprizi (R8) T011'de erken yakalanır — sorun çıkarsa US1'e girmeden çözülür.