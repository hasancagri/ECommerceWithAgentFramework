# Quickstart: ChatAgent Kalıcı Konuşma Memory'si — Canlı Doğrulama

Ön koşul: `dotnet run --project src/aspire/AppHost/AppHost.csproj` (tüm sistem; chatAgentDb dahil).
Kontratlar: [contracts/chat-conversations-api.md](contracts/chat-conversations-api.md).

## S1 — Kalıcılık (US1 / SC-001)

1. WebApp'te login ol, chat'te 2-3 mesajlaş (sepete ürün ekletmek iyi: araç çağrısı da kaydolsun).
2. AppHost'u kapat, yeniden başlat; aynı sohbette devam et.
3. Bekle: bağlam korunur; `chatAgentDb`'de conversation + item satırları durur; mesaj kaybı yok.

## S2 — Geçmiş listesi + tam görüntüleme (US2 / SC-002, SC-004)

1. Farklı konularda 2-3 ayrı sohbet aç ("yeni sohbet" ile).
2. Chat panelinde liste: son aktiviteye göre sıralı, başlıklar ilk mesajdan.
3. Eski bir sohbeti aç: TÜM mesajlar gelir (araç çağrıları dahil); liste/açılış < 2 sn.

## S3 — Sahiplik izolasyonu (SC-003)

1. İkinci bir kullanıcıyla login ol: liste boştur; ilk kullanıcının konuşması görünmez.
2. İlk kullanıcının conversation id'siyle `GET /v1/my-conversations/{id}/items` dene → 404.

## S4 — Anonim süreklilik + TTL (US3 / SC-006)

1. Logout/gizli pencere: anonim sohbet et, sayfayı yenile, devam et → bağlam korunur.
2. Anonim konuşma hiçbir listede yok. TTL: `Chat:AnonymousTtlHours`'u küçült (ör. 0),
   süpürücü tikini bekle → DB'den silinir; login konuşmaları durur.

## S5 — Pencere (FR-005 / SC-005)

1. `Chat:ContextWindowItems`'ı küçült (ör. 4); 6+ mesajlık sohbette ilk mesajdaki detayı sor.
2. Bekle: asistan hatırlamaz (pencere dışı) ama UI'da tüm mesajlar görünür; cevap süresi kısa
   sohbetle aynı mertebede.

## S6 — Depo hatası açık hatadır (FR-011)

1. Postgres container'ını durdur; mesaj göndermeyi dene.
2. Bekle: kullanıcıya açık hata; sistem sessizce RAM'e düşmez (restart sonrası "hayalet" sohbet yok).

## Birim testler

`dotnet test tests/ChatAgent.Tests` — başlık türetme, pencere seçimi, TTL filtresi, sahiplik kuralı.