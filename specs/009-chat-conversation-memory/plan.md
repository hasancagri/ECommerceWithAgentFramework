# Implementation Plan: ChatAgent Kalıcı Konuşma Memory'si

**Branch**: `009-chat-conversation-memory` | **Date**: 2026-07-24 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/009-chat-conversation-memory/spec.md`

## Summary

Konuşma geçmişi RAM'den çıkar, `chatAgentDb`'de Marten'a taşınır. WebApp `previous_response_id`
yerine **conversation id** taşır. Login kullanıcı konuşmalarını süresiz listeler/açar; anonim aynı
oturumda sürer, 24s aktivitesizlikte silinir. Modele son N item gider; depo ve UI eksiksizdir.
**Pivot notu (research'te)**: MAF Conversations depolama arayüzleri internal çıktı; akış kendi
`/v1/chat` SSE ucumuz + `AgentSession`'a Marten'dan geçmiş enjeksiyonuyla kuruldu (public API).

## Technical Context

**Language/Version**: .NET 10 / C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Microsoft.Agents.AI.Hosting.OpenAI 1.11.1-alpha (`IConversationStorage`,
`IAgentConversationIndex`, `CreateResponse.Conversation`), Marten 9.5.0 (Newtonsoft)

**Storage**: Yeni Postgres DB `chatAgentDb` (AppHost'ta yeni resource; kimseyle paylaşılmaz)

**Testing**: xUnit + Shouldly; yeni `tests/ChatAgent.Tests` projesi (saf birim testler)

**Target Platform**: Aspire ile ayağa kalkan chat-agent servisi + WebApp BFF/widget

**Project Type**: Mevcut mikroservis çözümünde servis içi genişleme (ChatAgent + WebApp dokunuşu)

**Performance Goals**: Liste/açılış < 2 sn (SC-004); uzun sohbette turn süresi sabit mertebe (SC-005)

**Constraints**: Sessiz in-memory fallback yasak (FR-011); depo kırpılmaz, pencere yalnız model input

**Scale/Scope**: Tek WebApp istemcisi; kullanıcı başına yüzlerce konuşma, konuşma başına yüzlerce item

## Constitution Check

- **I. BC İzolasyonu**: UYUMLU — `chatAgentDb` yalnız ChatAgent'ındır; başka servis dokunmaz.
  ChatAgent servislerle yine yalnız MCP üzerinden konuşur.
- **II. Zengin Aggregate**: SAPMA (gerekçeli) — ChatAgent bir bounded context değil, agent
  uygulamasıdır; `ConversationDocument` altyapı deposudur, iş kuralı taşımaz. Bkz. Complexity.
- **III. Vertical Slice + CQRS, repository yok**: UYUMLU (ruhen) — Wolverine yok; uçlar Minimal API
  extension'ında, depolar Marten `IDocumentSession`'a doğrudan yazar; repository katmanı yok.
- **IV. Result Pattern**: KISMİ — framework uçları OpenAI kontratı döner (değiştirilemez);
  bizim `my-conversations` uçları basit DTO + doğru HTTP kodu döner. Bkz. Complexity.
- **V. Scope-tabanlı yetki, rol yok**: UYUMLU — rol yok. Yeni scope da yok: uçlar geçerli JWT ister,
  veri erişimi kaynak-sahipliğiyle (token'daki `sub` = OwnerUserId) süzülür (araştırma R4).
- **Teknoloji kısıtları**: CPM ✓, Scrutor kullanılmaz (framework arayüzleri elle Replace edilir —
  marker arayüzü takılamaz), GlobalUsings ✓, agent'lar Singleton kalır ✓.

**Gate sonucu**: GEÇTİ (iki gerekçeli sapma Complexity Tracking'de).

## Project Structure

### Documentation (this feature)

```text
specs/009-chat-conversation-memory/
├── plan.md, research.md, data-model.md, quickstart.md
├── contracts/chat-conversations-api.md
└── tasks.md (/speckit-tasks üretir)
```

### Source Code (repository root)

```text
src/agents/ChatAgent/
├── Conversations/
│   ├── ConversationDocument.cs          # depo dokümanları (konuşma + item)
│   ├── ConversationRules.cs             # saf yardımcılar: başlık türetme, pencere, TTL filtresi
│   ├── ConversationStore.cs             # Marten deposu (pencere, append, TTL) — pivot
│   ├── ChatStreamEndpoint.cs            # POST /v1/chat SSE: geçmiş→session→koşu→kalıcılaştır — pivot
│   ├── MyConversationsEndpoints.cs      # create/list/items uçları (sahiplik burada)
│   └── AnonymousConversationCleanup.cs  # 24s TTL süpürücü (BackgroundService)
├── Program.cs                           # Marten + storage kayıtları + uçlar
src/aspire/AppHost/AppHost.cs            # chatAgentDb resource + referans
src/others/Common/.../SchemaConstants    # ChatAgent şema adı
src/ui/WebApp/Chat/ChatEndpoints.cs      # conversation id akışı + liste/items proxy
src/ui/WebApp/wwwroot (chat widget)      # sohbet listesi, yeni sohbet, id saklama
tests/ChatAgent.Tests/                   # ConversationRules birim testleri
```

**Structure Decision**: ChatAgent app-içi `Conversations/` klasörü tek sorumlu birimdir; WebApp
yalnız BFF proxy + widget dokunuşu alır. Yeni proje yalnız test projesidir.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Zengin aggregate yok (II) | Konuşma verisi iş kuralı taşımayan app-altyapı kaydı | ChatAgent'ı BC'ye çevirmek yapay domain üretir; kural yok, invariant yok |
| Result pattern kısmi (IV) | Framework uçları OpenAI kontratına kilitli | Sarmalamak istemciyi (widget) OpenAI şeklinden koparır, çeviri katmanı ekler |