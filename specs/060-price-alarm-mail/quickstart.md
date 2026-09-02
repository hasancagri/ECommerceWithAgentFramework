# Quickstart: Fiyat Alarmı + Mail Bildirimi (060)

Canlı doğrulama rehberi — implementasyon sonrası uçtan uca kanıt.

## Önkoşullar

- Docker açık (Postgres, RabbitMQ, Redis, **Mailpit** container'ları).
- NotificationAgent için OpenAI secret:
  `dotnet user-secrets set OpenAI:ApiKey <k> --project src/agents/NotificationAgent` (+ `OpenAI:Model`, ör. gpt-4o-mini).
- Build: `dotnet build` temiz.

## Başlatma

```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

- Aspire dashboard'da `library-api`, `notification-agent`, `mail-mcp`, `mailpit` YEŞİL olmalı.
- Mailpit UI: dashboard'daki `mailpit` http endpoint'i (container port 8025).

## Senaryo 1 — Alarm kur/kaldır (US1)

1. WebApp'te müşteri login → bir ürün detayına git (`/products/{id}`).
2. "Fiyat Alarmı Ekle" → sayfa "Alarm Kurulu" durumuna döner (SC-001).
3. Yeniden bas/kaldır → düğme ilk hâline döner.
4. Çıkış yap → anonimken düğme login'e yönlendirir, girişten sonra detaya dönülür.

## Senaryo 2 — Fiyat değişince mail (US2)

1. Alarmı kur (Senaryo 1).
2. Admin ile ürünün fiyatını DEĞİŞTİR (admin düzenleme ekranı, 058).
3. ≤1 dk içinde Mailpit UI'da mail görünmeli (SC-002); içerik birebir kontrol: hitap, ürün adı, ESKİ + YENİ fiyat, `/products/{id}` bağlantısı, Türkçe (SC-005).
4. Fiyatı BİR DAHA değiştir → İKİNCİ mail gelir (alarm yaşıyor — FR-004).
5. Alarmı kaldır → fiyatı değiştir → mail GELMEZ (SC-004).
6. Fiyat-dışı alan değiştir (ör. açıklama) → mail GELMEZ (SC-004).

## Senaryo 3 — Bildirim izi (US3)

- Mail sonrası `libraryDb`'de (pgAdmin) `library` şemasında `NotificationRecord` satırı: UserId + ProductId + `Success=true, Detail="sent"` (FR-007).

## Hata yolu (FR-008)

- Mailpit container'ını durdur → fiyat değiştir → NotificationAgent loglarında `NotificationException` + Wolverine retry (10s/30s/60s); tükenince mesaj error queue'da (RabbitMQ management UI).
- Mailpit'i geri aç → yeni fiyat değişimi normal akar.

## Birim testler

```bash
dotnet test tests/Library.Api.Tests/Library.Api.Tests.csproj   # PriceAlarm domain (test-first, İLKE VI)
scripts/check-flow-links.sh                                    # yeni FLOW.md anchor'ları
scripts/check-claude-spec-links.sh                             # BC haritası guard
```