# Quickstart: 041 Canlı Doğrulama

Ön koşul: Docker açık; `OpenAI:ApiKey`+`OpenAI:Model` Procurement user-secrets'ta (enrich fail-fast).
Temiz başlangıç önerilir (DB volume reset — ürünler yalnız feed'den).

## 1. Sistemi kaldır

```bash
dotnet build
dotnet run --project src/aspire/AppHost/AppHost.csproj
```

- Aspire panosunda `procurement-api` SAĞLIKLI; `supplier-gateway` ve `ingestion-agent` YOK (söküm doğrulaması).

## 2. İlk ingest (SC-002, SC-005, SC-006)

```bash
curl -X POST http://localhost:<procurement>/v1/feeds/pull   # veya Hangfire cron'u bekle
```

- WebApp vitrin: **3000 benzersiz ürün** (SC-002). Örnek üründe kategori kanonik ağaçtan (SC-006).
- Procurement logları: yapısal yolda AI çağrısı YOK; enrich yalnız eksik satırlarda (~300) koştu (SC-005).
- RabbitMQ panosu: `procurement.enrich` error queue BOŞ; `catalog.procurement-events` / `stock.procurement-events` drenajlı.

## 3. Buy-box fiyat kontrolü (SC-003)

- Çakışan barkod seç (8690000002501..3000 arası), Procurement havuzunda iki listing'i gör (pgAdmin →
  `procurementManagement.mt_doc_poolproduct`), vitrin fiyatının stoklu en ucuzla eşleştiğini doğrula.
- Eşit fiyatlı örnekte kazanan = Priority 1 (supplier-a).

## 4. Kazanan devri (SC-004)

```bash
curl -X POST http://localhost:<supplier-api>/v1/feeds/supplier-a/advance
curl -X POST http://localhost:<procurement>/v1/feeds/pull
```

- Fiyatı değişen çakışan üründe vitrin fiyat/stok güncellenir; kazananı stoksuz kalan üründe sonraki en ucuz devralır.
- Tüm offer'ları stoksuz kalan örnek: ürün vitrinde KALIR, stok 0, sepete eklenemez.

## 5. Idempotency + sıra bağımsızlığı (SC-007, SC-008)

- Aynı pull'u tekrar çalıştır → log "0 yayın"; RabbitMQ'da yeni mesaj yok; embedding yeniden üretimi yok.
- (Opsiyonel, temiz DB) Önce B sonra A çek → aynı kanonik içerik + aynı buy-box (Priority-merge sıra-bağımsız).

## 6. DLQ yolu (US3-5)

- Geçici: enrich handler'a bozuk API key ver → retry 10s/30s/60s → error queue'da mesaj içeriğiyle görünür;
  kalan satırlar işlenmeye devam eder. Key'i düzelt, mesajı replay et → yayın tamamlanır.

## 7. Regresyon

```bash
dotnet test    # Procurement.Api.Tests dahil tüm çözüm yeşil (SC-001)
```

- Chat'ten ürün sorgusu çalışır (okuma MCP tool'ları yerinde); sepet + checkout akışı değişmedi
  (Stock OnHand güncel → rezervasyon/commit aynı).

Kontratlar: [contracts/integration-events.md](contracts/integration-events.md) ·
Model: [data-model.md](data-model.md) · Kararlar: [research.md](research.md)