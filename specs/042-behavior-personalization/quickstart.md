# Quickstart: Davranış-Bazlı Kişiselleştirme (042) — Canlı Doğrulama

Uçtan uca kanıt: gezinti → JSONL → personalizationDb → eğitim → öneri yanıtı.

## Önkoşullar

- Python 3.12 + `uv` kurulu (`brew install uv`).
- Personalization venv hazır:
  ```bash
  cd src/services/personalization && uv venv && uv pip install -e .
  ```
- Sistem Aspire'dan ayağa:
  ```bash
  dotnet run --project src/aspire/AppHost/AppHost.csproj
  ```
- Dashboard'da `personalization` resource'u Running; `personalizationDb` hazır.

## Adımlar

1. **Sinyal üret** — tarayıcıda WebApp: ana sayfa aç (ListShown), 2-3 ürün detayı gez
   (ProductViewed), arama yap (SearchPerformed), login olup bir ürünü sepete ekle (BasketItemAdded).

2. **JSONL doğrula** — `artifacts/behavior-logs/behavior-<bugün>.jsonl` içinde satırlar var;
   alanlar [kontrata](contracts/behavior-log-line.md) uygun (anonim satırda userId YOK; kişisel
   veri YOK).

3. **Ingest doğrula** —
   ```bash
   curl -X POST http://localhost:<port>/v1/admin/ingest
   ```
   Yanıtta `processedLines` > 0. pgAdmin/psql: `behavior_events` satır sayısı = JSONL geçerli satır
   sayısı. Aynı komutu TEKRAR çalıştır: `processedLines: 0`, tablo satır sayısı DEĞİŞMEZ (SC-006).

4. **Eğitim doğrula** —
   ```bash
   curl -X POST http://localhost:<port>/v1/admin/train
   ```
   Yanıt `status: Succeeded`; `model_runs`'ta satır; `popular_products` dolu.

5. **Öneri doğrula** —
   - Gezinen kimlikle: `GET /v1/recommendations?anonymousId=<pz_aid>` → `source: personal|session`,
     boş olmayan `productIds`.
   - Uydurma kimlikle: `GET /v1/recommendations?anonymousId=<rastgele-guid>` → `source: popular`,
     boş olmayan liste (FR-013).

6. **Dayanıklılık** — dashboard'dan `personalization`'ı durdur; WebApp'te gezinmeye devam et:
   sayfalar normal (SC-005), JSONL büyüyor. Servisi başlat + ingest tetikle: biriken satırlar işlenir.

## Beklenen sonuç özeti

| Kontrol | Beklenen |
|---------|----------|
| JSONL satırları | Kontrata uygun, kişisel veri yok |
| Çift ingest | Satır sayısı sabit (%0 çift) |
| Eğitim | Succeeded + popular_products dolu |
| Tanınan kimlik önerisi | personal/session kaynaklı, boş değil |
| Tanınmayan kimlik | popular fallback, boş değil |
| Servis kapalıyken gezinti | Alışveriş akışı etkilenmez |

## Testler

```bash
cd src/services/personalization && uv run pytest        # Python birim testleri
dotnet test tests/WebApp.Tests/WebApp.Tests.csproj      # BehaviorEvent satır kontrat testi
```