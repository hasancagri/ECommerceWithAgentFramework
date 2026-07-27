# Research: IngestionAgent LLM-Sürücülü Yazıcılar (015)

**Date**: 2026-07-27 | **Plan**: [plan.md](plan.md)

Teknik bağlamda NEEDS CLARIFICATION kalmadı; aşağıdaki kararlar keşif (ChatAgent deseni,
mevcut IngestionAgent, hedef MCP tool'ları) üzerinden verildi.

## R1 — LLM agent iskeleti

- **Decision**: Yazıcı adım başına bir `ChatClientAgent` (Microsoft.Agents.AI); tek paylaşılan `IChatClient` (OpenAI).
- **Rationale**: ChatAgent'ta kanıtlı desen; FR-009 (adım-başına tool scope'u) agent-başına tool listesiyle doğal sağlanır; FR-010 tek istemci.
- **Alternatives**: Elle `IChatClient` function-calling döngüsü (MAF varken gereksiz kod); üç adıma tek agent (FR-009 scope ihlali).

## R2 — Tipli sonuç (WriterResult)

- **Decision**: Agent'a JSON-şemalı response format verilir; final metin `WriterResult`'a deserialize edilir. Spike doğrular.
- **Rationale**: FR-011 tipli sonuç ister; tool zarfını artık LLM okur, kod yalnız kendi sözleşmemizi deserialize eder (ayna değil).
- **Alternatives**: Final metni serbest parse (kırılgan); tool sonucunu koddan okumak (zarf parse geri gelir — FR-011/FR-012 ihlali).

## R3 — Short-circuit + terminal semantiği

- **Decision**: Conditional edge'ler + her yoldan beslenen tek terminal collector; FR-015 spike'ı implementasyonun ilk görevi.
- **Rationale**: Başarısız adım sonrası sonraki executor'lar hiç tetiklenmez (FR-003 harfiyen); terminal her yolda koşar (FR-005, S4 emsali).
- **Alternatives**: Bugünkü pass-through guard (executor koşar ama LLM çağrılmaz). Spike başarısız olursa fallback budur; plan notuyla kabul.
- **Spike sonucu (2026-07-27, T003 — GEÇTİ)**: `AddEdge<RecordJob>(src, dst, koşul)` teslimi keser (sonraki executor hiç koşmaz); terminal her
  yolda tetiklenir; koşullu edge'li run askıda kalmadan tamamlanır. Kanıt: tests/IngestionAgent.Tests/WorkflowSemanticsSpikeTests.cs (4 test).
  Fallback'e gerek kalmadı; rewiring conditional edge + terminal collector ile yapılır.

## R4 — Anonim MCP tool → AIFunction

- **Decision**: `PerUserMcpTool`'un token'sız kopyası `AnonymousMcpTool` + `McpToolCatalog` (lazy discovery + allowlist); çağrı başına taze MCP session.
- **Rationale**: ChatAgent ile tekdüzelik; lazy discovery açılış-sırası bağımlılığını kaldırır; taze session `Invalidate` makinesini gereksiz kılar.
- **Alternatives**: Ortak kütüphaneye çıkarma (2 kopya için erken — agent-constants kararıyla tutarlı); bağlantı cache'i (invalidasyon karmaşası).

## R5 — Zaman bütçesi

- **Decision**: Adım başına (LLM+tool döngüsü) timeout config'ten (`Ingestion:StepTimeoutSeconds`, default 60); retry cooldown'ları değişmez.
- **Rationale**: LLM gecikmesi bugünkü 15s MCP timeout'una sığmaz; timeout'suz adım `WORKFLOW_INCOMPLETE` tespitini geciktirir.
- **Alternatives**: 15s'i korumak (LLM için dar); tek global timeout (hangi adımın taştığı görünmez).
- **Canlı bulgu (2026-07-27, T014 sonrası)**: 3×60s adım toplamı Wolverine'in varsayılan 60s execution timeout'unu AŞAR → dış iptal
  S4-varyantı belirsiz yol açar. Düzeltme: `opts.DefaultExecutionTimeout = 4dk` (adım-içi timeout'lar asıl bekçi kalır).

## R6 — Determinizm önlemleri

- **Decision**: Temperature 0, adım başına dar allowlist (1–2 tool), sınırlı tool-iterasyonu; prompt: tool zorunlu, uydurma yasak, hata → kod.
- **Rationale**: Yazma yolunda varyansı ve sahte-başarı riskini kısar; tam giderme (geri-okuma) bilinçle kapsam dışı (spec Assumptions).
- **Alternatives**: Deterministik geri-okuma doğrulaması (gelecek sertleştirme adayı); serbest prompt (non-determinizm artar).

## R7 — Model config

- **Decision**: `OpenAI:ApiKey` ve `OpenAI:Model` ikisi de zorunlu; yokluğunda açılışta throw (FR-014). Kaynak: user-secrets/appsettings.
- **Rationale**: FR-014 fail-fast ister; yazma yolunda örtük model default'u sessiz sürüklenme riskidir (ChatAgent'taki default bilinçle alınmadı).
- **Alternatives**: ChatAgent gibi model default'u (reddedildi); AppHost'tan env enjeksiyonu (gereksiz — ChatAgent mekanizmasıyla aynı kalsın).

## R8 — Temizlik ve yapı

- **Decision**: `McpConnector` + `McpToolInvoker` (ve `ToolData`/`ToolMessage`/`ToolResponse`/`ToolOutcome`) silinir; klasörler akış sırasına numaralanır.
- **Rationale**: FR-012/SC-005 (0 zarf ayna tipi); bugünkü numara-akış uyumsuzluğu (02=Discount, 03=Stock) okuyucuyu yanıltıyor.
- **Alternatives**: Ölü kodu bırakmak (SC-005 ihlali); klasörlara dokunmamak (ucuz düzeltme fırsatı kaçar).