# Quickstart / Doğrulama: Kampanya İndirim Motoru (Discount.Api)

Feature'ın uçtan uca çalıştığını kanıtlayan senaryolar. Sistem **Aspire AppHost'tan** başlatılır
(`dotnet run --project src/aspire/AppHost/AppHost.csproj`) — Discount.Api + discountDb + RabbitMQ +
Storefront + Order + Identity aynı grafikte.

## Ön koşullar

- Katalogda yayınlı kitaplar (051/080 import) + Storefront view dolu (`query_storefront` çalışıyor).
- `discountDb` şeması açılışta kurulur (Marten `ApplyAllDatabaseChangesOnStartup`).
- Admin OAuth istemcisi (`external-admin-agent`) `AdminDiscountWrite` scope'u + `/mcp-admin` erişimi.
- Order.Api makine token'ı `discount.read` taşır (SagaTokenHandler).

## Senaryo 1 — Kategori süzgeciyle indirim → vitrinde inline (US1, P1)

1. Admin `/mcp-admin` → `admin_create_campaign(name:"Roman Bahar", scopeType:"category",
   scopeRef:<roman-categoryId>, percentage:20, startsAt:now, endsAt:now+7g)`.
2. **Beklenen**: MCP yanıtı kısa özet — `{applied: N, skipped: M}`. Kitap listesi dönmez.
3. Müşteri asistanı `query_storefront`("roman") çağırır.
4. **Beklenen**: Uygulanan her kitap `discount_pct=20`, `effective_price = liste × 0.8`, `discount_ends_at`
   dolu (≤5 sn — SC-001). İndirimsiz/atlanan kitap liste fiyatı + boş discount alanları.

## Senaryo 2 — Kitap başına tek indirim / atla (US1 AC-2, SC-003)

1. Bir Roman kitabına önce `admin_create_campaign(scopeType:"product", scopeRef:<kitapId>, percentage:30, ...)`.
2. Sonra `admin_create_campaign(scopeType:"category", scopeRef:<roman>, percentage:20, ...)`.
3. **Beklenen**: O kitap %30'da KALIR (kategori uygulaması onu ATLAR); özet `skipped` sayar. Hiçbir kitapta
   iki indirim yok.

## Senaryo 3 — Yazar / yayınevi süzgeci (US1 AC-3)

1. `admin_create_campaign(scopeType:"author", scopeRef:<yazarId>, percentage:15, ...)`.
2. **Beklenen**: O yazarın (indirimsiz) tüm yayınlı kitapları %15 indirimli döner. `publisher` süzgeci aynı davranış.

## Senaryo 4 — Süre bitişi → otomatik temizlik (US2)

1. `endsAt`'i yakın (ör. now+2dk) kampanya aç; kitaplar indirimli görünür.
2. `endsAt` geç.
3. **Beklenen**: `CampaignEnded` fire → o campaign'in `ProductDiscount`'ları silinir →
   `ProductDiscountChanged(pct:0)` → Storefront satırları temizlenir; `query_storefront` liste fiyatı döner.
   Elle müdahale yok. (Fire gecikirse view-guard `now>ends_at` indirimi zaten gizler.)

## Senaryo 5 — Checkout canlı doğrulama + sepette grace yok (US1 AC-4, US3)

1. İndirimli kitabı sepete ekle; `start_payment` (Order.Api) çağır.
2. **Beklenen (aktifken)**: Order tutarı Discount.Api gRPC'den gelen yüzdeyle indirimli (SC-004).
3. Kampanya sepetteyken biter, sonra `start_payment`.
4. **Beklenen**: gRPC aktif yüzde döndürmez → tutar LİSTE fiyatı (grace yok).

## Senaryo 6 — İptal → temizlik (edge)

1. Aktif kampanya → `admin_cancel_campaign(campaignId)`.
2. **Beklenen**: O campaign'in kitaplarının indirimi anında temizlenir (push); vitrin liste fiyatı.

## Domain testleri (İLKE VI — test-first)

- Campaign.Create: yüzde 0/100/150 red; endsAt<startsAt red; boş name red.
- Campaign.Cancel: Status=Cancelled → IsEffectiveAt hep false.
- CampaignSelectionResolver: kategori/yazar/yayınevi süzgeci doğru kitap setini döner; tek-kitap doğrudan;
  çözülmeyen süzgeç boş set.
- Apply-skip: zaten `ProductDiscount`'u olan kitap atlanır (insert-if-not-exists); yoksa eklenir.

## Guard'lar

- `scripts/check-agent-query-schema.sh` — yeni discount kolonları `query_storefront` SchemaBlock'ta olmalı.
- `scripts/check-flow-links.sh` — `src/services/discount/FLOW.md` anchor tip adları kodda var olmalı.