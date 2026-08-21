# Implementation Plan: Davranış-Bazlı Kişiselleştirme (Personalization BC)

**Branch**: `042-behavior-personalization` | **Date**: 2026-08-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/042-behavior-personalization/spec.md`

## Summary

WebApp sunucu tarafı gezinti sinyallerini (görüntüleme/impression/arama/sepete-ekleme) versiyonlu
JSONL davranış loguna yazar. Yeni Personalization BC — sistemin ilk .NET-dışı servisi (Python/FastAPI,
Aspire resource) — bu dosyaları idempotent biçimde kendi `personalizationDb`'sine indirir, zamanlanmış
job ile implicit-ALS modeli eğitir ve `GET /recommendations` ucundan top-N ürün döner (popüler
fallback'li). UI gösterimi kapsam dışı; uç, doğrulama + gelecek tüketici yüzeyidir.

## Technical Context

**Language/Version**: C# / .NET 10 (WebApp yakalama); Python 3.12 (Personalization servisi)

**Primary Dependencies**: .NET: yok (yeni paket eklenmez; özel JSONL yazıcı — bkz. research R1).
Python: FastAPI + uvicorn, psycopg 3, implicit (ALS) + scipy, APScheduler. Aspire.Hosting.Python
13.3.5 (AppHost; tüm Aspire.Hosting.* sürüm-eş kuralı).

**Storage**: `personalizationDb` (Aspire Postgres, tek sahibi Python servisi; Marten YOK — BC .NET
değil). Taşıma: gitignore'lu paylaşımlı dizinde günlük JSONL dosyaları. Model: yerel dosya (.npz).

**Testing**: Python: pytest (parser, idempotent ingest, fallback, mini-eğitim smoke). .NET: xUnit +
Shouldly (BehaviorEvent satır serileştirme). E2E kapsam dışı (anayasa E2E listesi değişmiyor).

**Target Platform**: macOS dev, Aspire AppHost orkestrasyonu (Python lokal süreç + venv)

**Project Type**: Mikroservis sistemine yeni polyglot servis + mevcut WebApp'e yakalama katmanı

**Performance Goals**: Öneri sorgusu < 500 ms (SC-003); sinyal → depoda sorgulanabilir < 1 dk (SC-001)

**Constraints**: Yakalama sayfa akışını bloklamaz (FR-008, arka planda async yazım); sayfa başına
canlı LLM çağrısı yasak; mevcut .NET servislerine dokunuş sıfır (yalnız WebApp + AppHost).

**Scale/Scope**: Dev ölçeği (tek makine, küçük veri); hedef model kalitesi değil uçtan uca pipeline.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Durum | Not |
|------|-------|-----|
| I. BC izolasyonu | ⚠️ GEREKÇELİ SAPMA | JSONL dosya kanalı I'in kanal listesinde yok — bkz. Complexity Tracking |
| I. DB izolasyonu | PASS | personalizationDb'ye yalnız Python servisi bağlanır; .NET hiç bağlanmaz |
| II. Zengin aggregate | N/A (.NET domain yok) | BC Python; veri = ham event + model meta, aggregate/invariant taşımaz |
| III. Vertical Slice + CQRS | N/A (.NET domain yok) | WebApp tarafı yalnız yakalama servisi; slice değişikliği yok |
| IV. Result pattern | PASS | .NET tarafında yeni handler yok; FastAPI kendi hata sözleşmesini kullanır |
| V. Scope yetkilendirme | PASS (bilinçli anonim) | /recommendations anonim okuma (sistemdeki anonim vitrin okumalarıyla tutarlı) |
| VI. Domain-TDD | PASS (uyarlanmış) | Saf mantık (parser, offset, fallback) pytest ile test-first; aggregate yok |
| Aspire tek giriş | PASS | Python servisi AppHost resource'u; bağımsız çalıştırılmaz |
| Options pattern | PASS | WebApp'te BehaviorLogOptions POCO (AddOptionsExt); IConfiguration doğrudan okunmaz |
| Central Package Mgmt | PASS | Aspire.Hosting.Python 13.3.5 Directory.Packages.props'a eklenir |

**Post-Phase-1 yeniden değerlendirme**: PASS (sapma Complexity Tracking'de gerekçeli; amendment
İŞLENDİ — anayasa v1.9.0, İlke I telemetri kanalı istisnası). Yeni ihlal doğmadı.

## Project Structure

### Documentation (this feature)

```text
specs/042-behavior-personalization/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 çıktısı
├── data-model.md        # Phase 1 çıktısı
├── quickstart.md        # Phase 1 çıktısı
├── contracts/
│   ├── behavior-log-line.md    # JSONL satır kontratı (v1)
│   └── recommendations-api.md  # REST kontratı
└── tasks.md             # Phase 2 (/speckit-tasks üretir)
```

### Source Code (repository root)

```text
src/aspire/AppHost/
└── AppHost.cs                          # + personalizationDb, + python resource, + env kablolama

src/ui/WebApp/
├── Services/Behavior/
│   ├── BehaviorEvent.cs                # log satırı DTO'su (kontratın C# yüzü)
│   ├── BehaviorLogWriter.cs            # Channel<T> + arka plan JSONL yazıcı (ISingletonDependency)
│   └── AnonymousIdMiddleware.cs        # kalıcı AnonymousId + SessionId çerezleri
├── Options/BehaviorLogOptions.cs       # dizin, dosya öneki, aç/kapa
└── Pages/…                             # Products detay/liste, Index, arama, Basket ekleme handler'ları

src/services/personalization/
├── pyproject.toml                      # bağımlılıklar (uv/pip uyumlu)
├── main.py                             # FastAPI app + lifespan (scheduler + şema init)
├── config.py                           # env'den ayarlar (Aspire enjeksiyonu)
├── db.py                               # psycopg bağlantı + şema init + sorgular
├── ingest.py                           # JSONL okuyucu (offset-takipli, idempotent)
├── train.py                            # implicit ALS eğitimi + model dosyası + popüler liste
├── recommend.py                        # skorlama + fallback kararı
└── tests/
    ├── test_ingest.py                  # parse + idempotency + bozuk satır
    ├── test_recommend.py               # fallback + kişisel yol
    └── test_train.py                   # mini sentetik veriyle eğitim smoke

tests/WebApp.Tests/ (yeni, küçük)
└── BehaviorEventTests.cs               # satır serileştirme kontrat testi
```

**Structure Decision**: Mevcut `src/services/<bc>` düzenine uyularak Python servisi
`src/services/personalization/` altına konur (çözüm dosyasına girmez — .NET projesi değil).
WebApp'e yalnız yakalama katmanı eklenir; hiçbir mevcut .NET servisi değişmez.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| İlke I kanal listesi dışı JSONL dosya taşıma (WebApp → Personalization) | Tek üretici + tek tüketici + kayıp-toleranslı telemetri; event kontratı/RabbitMQ töreni bu iş için ağır | Integration event: Wolverine+exchange+kontrat bakımı gerektirir, davranış verisi domain gerçeği değil telemetri; REST ingest: senkron bağlaşma + Python kapalıyken veri kaybı. Not: WebApp BC DEĞİL (DB'siz UI/BFF); sapma "BC-arası" değil "UI→BC besleme" olarak dar yorumlanır. Amendment önerisi: research.md R7 |