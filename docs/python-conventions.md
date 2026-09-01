# Python Konvansiyonları (taşınabilir katman)

Bu dosya, projedeki **Python mikroservisleri** için `docs/conventions.md`'nin karşılığıdır. Amaç: Python'u
**gelişigüzel değil, kendi idiomunun en yüksek disiplininde** yazmak. .NET conventions'ın DDD/VSA ilkelerini
**taşır**, C# yapı-taşlarını (AggregateRoot, IDocumentSession, Result-pattern töreni) **taşımaz**. **İLKE N** =
projenin `.specify/memory/constitution.md` ilkesi (anayasa Python BC'ye de uygulanır).

## Temel duruş

- **İlkeler taşınır, dil-özel taklit taşınmaz.** BC izolasyonu (İLKE I), saf test-edilebilir çekirdek (İLKE VI),
  FLOW.md (İLKE VII), VSA, port disiplini → Python'a da uygulanır. Marten/Wolverine **taklit edilmez**; desenler
  Python idiomuyla kurulur (referans: *Cosmic Python* — Repository/UoW/Message Bus/CQRS/Event Sourcing).
- **Mega-framework yok, birleştir.** .NET'in entegre yığını (Marten+Wolverine) yerine Python küçük araçları açıkça
  bağlar. Bu eksiklik değil, Python felsefesi. Over-tooling yapma (bu servis document-store/event-sourcing/outbox/saga İSTEMEZ).
- **Class hak edince, fonksiyon etmeyince.** Her şeyi class'a sokmak = "Python'da Java" = kaçınılan gelişigüzellik.

## Yığın (kilitli; sürümler `pyproject.toml`'da pinlenir)

- **Dil/runtime:** Python 3.12+ · **Paket/env:** `uv` (pip/poetry değil).
- **API:** FastAPI + Pydantic v2 · **Broker (RabbitMQ):** FastStream (**pre-1.0 → sürüm PİNLE**, 0.7.x).
- **DB (Postgres):** SQLAlchemy 2.0 (async) + Alembic (migration) · ML için pandas/polars'a çekilir.
- **Zamanlama:** APScheduler (servis-içi) · **ML:** scikit-learn + matplotlib (faz-1); torch (faz-2).
- **Host:** Aspire 13 `AddUvicornApp` (resmi `Aspire.Hosting.Python`); sistem hep AppHost'tan başlar.
- **Kalite (mekanik zorlama):** **ruff** (format+lint) · **pyright/mypy strict** (tip) · **pytest** + coverage.

## Yapı — VSA (dikey dilim) + hexagonal port

Servis kökü kendi kendine yeter (PyCharm ayrı açar; `.slnx`'e girmez). Python paketi snake_case.

```
src/services/RecoTrainer/            # PyCharm proje kökü
├── pyproject.toml  uv.lock
├── src/reco_trainer/
│   ├── features/                    # VSA: dikey dilim (feature başına)
│   │   ├── ingest_signals/          #   schema.py (Pydantic) + consumer.py (FastStream) + store fonksiyonu
│   │   └── build_profile/           #   schema.py + pipeline.py (SAF ML) + endpoint.py (FastAPI) + service.py
│   ├── domain/                      # PAYLAŞILAN saf mantık (value object + skorlama) — I/O yok
│   ├── adapters/                    # outbound port: postgres repo, broker publisher
│   ├── config.py                    # pydantic-settings (Options karşılığı)
│   └── app.py                       # composition root: FastAPI + FastStream + scheduler
├── tests/                           # pytest; domain/pipeline test-first (İLKE VI)
└── FLOW.md                          # İLKE VII (anchor = Python fonksiyon/sınıf adı)
```

- **Dilim kendi stack'ini taşır** (schema + entrypoint + mantık) — .NET slice'ının Python karşılığı.
- **CQRS töreni ZORLAMA:** command/query `[Transactional]` ayrımı C#'a özel. Dilim = feature-klasörü; training bir
  pipeline job, command/query değil. VSA'nın "feature kendi stack'i" ruhunu al, plumbing'i alma.

## Class vs fonksiyon (idiomatik disiplin)

- **Class:** Pydantic schema (DTO/command/event), domain entity / value object (`@dataclass` ya da Pydantic),
  stateful adapter (db/broker), config (pydantic-settings).
- **Fonksiyon (modülde):** saf ML transform (IDF fit, skor, evaluate), pure pipeline adımı. **Tek-metotlu class = code smell.**

## Kod standartları

- **Tip zorunlu:** her fonksiyon imzası tam tip-hint'li; `pyright`/`mypy` **strict** CI kapısı. "Random Python"ı önleyen ilk şey.
- **İsimlendirme:** PEP 8 — `snake_case` fonksiyon/değişken/modül, `PascalCase` class, `UPPER_SNAKE` sabit.
- **Hata:** Python idiomu = exception (Result-pattern C#'a özel, dayatma yok). Ama disiplinli: domain-özel exception
  tipleri, sessiz-yutma yok, beklenen-hata != beklenmeyen. Kayıp-toleranslı yol (telemetri) bilinçle yutar + loglar.
- **Async:** async/await; event-loop bloklama yok (CPU-ağır ML işi thread/process'e ya da zamanlanmış job'a).
- **Config:** pydantic-settings POCO; ortam değişkeninden DOĞRUDAN okuma yok (Options deseni karşılığı).
- **Docstring:** public fonksiyon/modül tek satır ne-yaptığı; FLOW.md anchor'larına köprü.
- **DI:** FastAPI `Depends` (constructor-injection karşılığı); global singleton yerine dependency provider.

## Test (İLKE VI Python'da)

- **Saf çekirdek test-first:** `domain/` + `features/*/pipeline.py` (skorlama, kümeleme, IDF, MMR) mock'suz → pytest, önce kırmızı.
- **Kapsam dışı (test-sonra/canlı):** consumer/endpoint/adapter/wiring — entegrasyon harness'ı ilk ihtiyaçla.
- **ML ölçümü ≠ birim testi:** kalite metrikleri (kapsama/çeşitlilik) ayrı; matplotlib çıktı gözlem için, assertion için değil.

## Servisler-arası (BC izolasyonu — İLKE I)

- **Tüketici (inbox/idempotent):** broker event'i eventId ile dedupe et; kendi feature store'una yaz. Başka BC DB'sine erişme.
- **Yayıncı:** profil/sonuç event'i best-effort + retry (downstream .NET idempotent). Outbox opsiyonel (faz-1 atla).
- **Sözleşme:** paylaşılan integration event şeması (broker) + sanksiyonlu REST. MCP'yi agent-dışı koddan sürme (anayasa).
- **FLOW.md:** domain süreci EventStorming altitude'unda; anchor = Python fonksiyon/sınıf adı; `check-flow-links` guard aynı mantık.
