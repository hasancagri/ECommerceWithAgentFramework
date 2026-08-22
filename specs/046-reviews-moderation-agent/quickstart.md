# Quickstart / Doğrulama: Reviews Moderasyon Agent Taşıma

Feature'ın uçtan uca çalıştığını kanıtlayan koşulabilir senaryolar. Tam sistem Aspire'dan açılır.

## Ön koşullar

- OpenAI secret **worker'a** verilir (Reviews'e DEĞİL):
  ```bash
  dotnet user-secrets set OpenAI:ApiKey <key> --project src/agents/Reviews.Moderation
  dotnet user-secrets set OpenAI:Model gpt-4o-mini --project src/agents/Reviews.Moderation
  ```
- Reviews.Api'nin OpenAI secret'ına artık ihtiyacı YOK (kaldırılır).

## Derleme + birim test

```bash
dotnet build
dotnet test tests/Reviews.Api.Tests/Reviews.Api.Tests.csproj
```
Beklenen: 0 hata; Reviews.Api.Tests tümü PASS (aggregate/VO davranışı değişmedi).

## Statik doğrulama (FR-002 — Reviews'te agent-framework yok)

```bash
grep -rn "Microsoft.Agents.AI\|ChatClientAgent\|OpenAIClient" src/services/reviews/Reviews.Api --include=*.cs | grep -v /obj/ | grep -v /bin/
grep -n "Microsoft.Agents.AI\|OpenAI" src/services/reviews/Reviews.Api/Reviews.Api.csproj
```
Beklenen: her ikisi de **boş** (sıfır eşleşme).

## Sistemi başlat

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```
Dashboard'da `reviews-moderation-agent` resource'u **Running**; `reviews-api` Running. Worker RabbitMQ'ya
bağlı (kendi `reviews-moderation.requested` kuyruğunu dinler).

## Senaryo 1 — Temiz yorum (US2)

1. Satın alınmış bir ürün için giriş yapıp temiz metinli yorum gönder.
2. Beklenen: yorum **anında Visible** (post-moderation); ürün sayfasında görünür.
3. Kısa süre sonra worker denetler → `ReviewModerated(Violation=false)` → yorum Visible kalır.

## Senaryo 2 — Sakıncalı yorum (US2)

1. Kişiye hakaret/küfür içeren metinle yorum gönder (ürüne sert eleştiri DEĞİL — o serbest).
2. Beklenen: yorum önce Visible; worker `Violation=true` verince **Hidden** olur; ürün özeti
   (ortalama+sayı) yeniden hesaplanıp Storefront'a yansır.
3. Ürüne "berbat ürün, almayın" gibi küfürsüz sert yorum → **Visible kalır** (ihlal değil).

## Senaryo 3 — Broker dayanıklılığı (US3, fail-open)

1. RabbitMQ container'ını durdur (Aspire dashboard / docker).
2. Yorum gönder → **submit başarılı**, yorum Visible görünür (reviewsDb'ye commit; istek outbox'ta).
3. RabbitMQ'yu geri başlat → bekleyen `ReviewModerationRequested` relay edilir, worker denetler,
   sonuç uygulanır. Hiçbir yorum kaybolmaz.
4. Worker'ı durdurup yorumlar gönder → hepsi Visible; worker dönünce hepsi denetlenir.

## Senaryo 4 — Metinsiz yorum (edge)

1. Yalnız yıldız (metin boş) ver.
2. Beklenen: `ReviewModerationRequested` **hiç yayınlanmaz**; yorum Visible kalır; worker'a trafik gitmez.

## Başarı ölçütleri eşlemesi

- SC-001/SC-002: statik doğrulama + dashboard'da worker process'i.
- SC-003: Senaryo 3 (broker down submit kaybı = 0).
- SC-004: Senaryo 1–2 (canlı smoke).
- SC-005: derleme + Reviews.Api.Tests PASS.
