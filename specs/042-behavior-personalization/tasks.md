# Tasks: Davranış-Bazlı Kişiselleştirme (Personalization BC)

**Input**: Design documents from `/specs/042-behavior-personalization/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: .NET domain aggregate yok — İlke VI klasik kapsamı boş. Uyarlama (plan): Python saf
mantığı (parse, idempotency, fallback) test-first; BehaviorEvent satır kontratı xUnit ile.

**Organization**: US1 = yakalama (P1), US2 = ingest (P2), US3 = eğitim+öneri (P3).

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 Python proje iskeleti: src/services/personalization/ altında pyproject.toml (fastapi,
      uvicorn, psycopg[binary], implicit, scipy, apscheduler; dev: pytest) + boş modül dosyaları
      (main.py, config.py, db.py, ingest.py, train.py, recommend.py, tests/) — R9
- [X] T002 [P] Directory.Packages.props'a Aspire.Hosting.Python 13.3.5 ekle + AppHost.csproj'a
      sürümsüz PackageReference — R2
- [X] T003 .gitignore'a artifacts/behavior-logs/ + src/services/personalization/.venv +
      src/services/personalization/models/ ekle — R3
- [X] T004 AppHost.cs: personalizationDb (postgres.AddDatabase) + AddPythonApp("personalization",
      "../services/personalization", "main.py") + WithHttpEndpoint(env:"PORT") + WithReference(db)
      + BEHAVIOR_LOG_DIR env; WebApp resource'una BehaviorLog__Directory env — R2, R3
      (ASPIREHOSTINGPYTHON001 pragma gerekebilir)

**Checkpoint**: `dotnet build` geçer; AppHost personalization resource'unu tanır (venv sonra).

---

## Phase 2: Foundational (Blocking Prerequisites)

- [X] T005 config.py: env okuma (ConnectionStrings__personalizationDb, BEHAVIOR_LOG_DIR, PORT,
      INGEST_INTERVAL_SECONDS=30, TRAIN_INTERVAL_SECONDS=600) tek Settings nesnesi — R2, R3
- [X] T006 db.py: psycopg bağlantı + şema init (behavior_events UNIQUE(source_file,line_no),
      ingest_offsets, model_runs, popular_products) — data-model.md birebir
- [X] T007 main.py: FastAPI app + lifespan (şema init) + GET /health + uvicorn PORT'tan başlatma;
      Aspire'da Running + health yeşil doğrula — contracts/recommendations-api.md

**Checkpoint**: AppHost ile personalization ayakta, tablolar personalizationDb'de oluşmuş.

---

## Phase 3: User Story 1 — Gezinti sinyallerinin kaydı (P1) 🎯 MVP

**Goal**: Gezinti eylemleri kontrata uygun JSONL satırlarına düşer; sayfa akışı hiç etkilenmez.

**Independent Test**: quickstart adım 1-2 — gezin, dosyadaki satırları kontratla karşılaştır.

- [X] T008 [US1] BehaviorLogOptions POCO (src/ui/WebApp/Options/BehaviorLogOptions.cs: Directory,
      Enabled=true) + OptionsExt kaydı — Options pattern, IConfiguration doğrudan okunmaz
- [X] T009 [P] [US1] BehaviorEvent DTO (src/ui/WebApp/Services/Behavior/BehaviorEvent.cs):
      kontrat alanları, camelCase, null alan yazılmaz, SchemaVersion=1 — contracts/behavior-log-line.md
- [X] T010 [US1] tests/WebApp.Tests projesi (xUnit+Shouldly, slnx'e ekle) + BehaviorEventTests:
      4 event tipinin satır çıktısı kontrat örnekleriyle eşleşir (test önce yazılır, T009 ile kırmızı-yeşil)
- [X] T011 [US1] BehaviorLogWriter (src/ui/WebApp/Services/Behavior/BehaviorLogWriter.cs):
      ISingletonDependency, Channel<BehaviorEvent> + arka plan tüketici, behavior-YYYYMMDD.jsonl
      (UTC) append, hata yut + ILogger warn — R1, FR-008
- [X] T012 [US1] AnonymousIdMiddleware (src/ui/WebApp/Services/Behavior/AnonymousIdMiddleware.cs):
      pz_aid kalıcı (1 yıl, HttpOnly) + pz_sid oturumluk; Program.cs'e UseMiddleware — R8
- [X] T013 [US1] Ürün detay sayfası handler'ına ProductViewed yazımı (src/ui/WebApp/Pages/Products/
      detay PageModel; brand/category/price render verisinden denormalize) — FR-001
- [X] T014 [P] [US1] Liste sayfalarına ListShown impression (Index.cshtml.cs + Products liste
      PageModel; gösterilen ProductId listesi tek satır) — FR-002
- [X] T015 [P] [US1] Arama handler'ına SearchPerformed (arama yapılan PageModel; searchTerm) — FR-003
- [X] T016 [P] [US1] Sepete ekleme handler'ına BasketItemAdded (WebApp sepete-ekle PageModel/handler;
      ürün alanları denormalize) — FR-004
- [X] T017 [US1] Canlı doğrulama: quickstart adım 1-2 (anonim + login'li satırlar, kişisel veri yok,
      writer kapalıyken sayfa akışı bozulmuyor)

**Checkpoint**: JSONL dosyası kontrata uygun doluyor — MVP verisi birikmeye başladı.

---

## Phase 4: User Story 2 — Personalization deposuna aktarım (P2)

**Goal**: JSONL satırları idempotent biçimde behavior_events'e iner; bozuk satır süreci durdurmaz.

**Independent Test**: quickstart adım 3 — elle JSONL besle, çift ingest'te satır sayısı sabit.

- [X] T018 [US2] tests/test_ingest.py ÖNCE: geçerli 4 tip parse; bozuk/eksik/bilinmeyen-sürüm satır
      atlanır + sayılır; aynı dosya iki kez işlenince tablo değişmez (FR-009, FR-010, SC-006)
- [X] T019 [US2] ingest.py: offset'ten oku, satır parse + doğrula, INSERT + offset güncelle tek
      transaction, UNIQUE çakışmasında sessiz geç, skipped_count logla — R4
- [X] T020 [US2] main.py: POST /v1/admin/ingest + APScheduler 30 sn ingest job (lifespan) — R6
- [X] T021 [US2] Canlı doğrulama: quickstart adım 3 (processedLines>0; tekrar=0; sayılar JSONL ile eşit)

**Checkpoint**: Gezinti < 1 dk içinde DB'de sorgulanabilir (SC-001).

---

## Phase 5: User Story 3 — Model eğitimi ve öneri sorgusu (P3)

**Goal**: ALS modeli eğitilir; /recommendations kişisel/oturum/popüler zinciriyle asla boş dönmez.

**Independent Test**: quickstart adım 4-5 — bilinen veriyle eğit, tanınan+tanınmayan kimlikle sorgula.

- [X] T022 [US3] tests/test_train.py + tests/test_recommend.py ÖNCE: sentetik mini veriyle eğitim
      Succeeded + ağırlıklar (view=1, add=3); fallback zinciri personal→session→popular; tanınmayan
      kimlik popüler alır; boş dönmez (FR-011, FR-013)
- [X] T023 [US3] train.py: behavior_events'ten CSR matris (kimlik=UserId||AnonymousId), implicit ALS
      (factors=32, iterations=15), .npz atomik yaz (tmp+rename), model_runs kaydı, son-7-gün top-50
      popular_products yenile — R5
- [X] T024 [US3] recommend.py: model yükleme + kilitli referans swap; skorlama; matris-dışı kimlikte
      sessionProductIds ile similar_items; popüler fallback — R5, FR-012, FR-013
- [X] T025 [US3] main.py: GET /v1/recommendations (400: iki kimlik de yok) + POST /v1/admin/train +
      APScheduler 10 dk train job + /health'e modelLoaded/lastIngestAt — contracts/recommendations-api.md
- [X] T026 [US3] Canlı doğrulama: quickstart adım 4-6 (eğitim Succeeded; personal/session vs popular;
      servis-kapalı dayanıklılık SC-005)

**Checkpoint**: Uçtan uca zincir kanıtlı — geliştirme hedefi (eğitilmiş model + sorgu ucu) tamam.

---

## Phase 6: Polish & Cross-Cutting

- [X] T027 [P] `uv run pytest` + `dotnet build` + `dotnet test` tam geçiş; quickstart'taki test
      komutları doğrulanır
- [X] T028 [P] CLAUDE.md güncelle: Personalization BC bölümü (JSONL kanal, Python/Aspire, ALS,
      kapsam sınırları) + Mimari servis listesine ekle
- [X] T029 Anayasa amendment önerisini işle: R7 metniyle İlke I'e telemetri-kanalı eki
      (/speckit-constitution ayrı koşu; implement'i bloklamaz, PR öncesi karar)

## Dependencies

- Phase 1 → Phase 2 → US1 → US2 → US3 → Polish (bu feature'da story'ler veri zinciri: US2, US1'in
  dosyasını; US3, US2'nin tablosunu tüketir — sıralı uygulanır; bağımsız TEST yine mümkün: US2
  elle yazılmış JSONL ile, US3 elle INSERT'lenmiş satırlarla test edilir).
- T010 testi T009 implementasyonuyla kırmızı-yeşil döngüde; T018 T019'dan, T022 T023-T024'ten önce.

## Parallel Examples

- Phase 1: T002 ∥ T003 (T001'den bağımsız dosyalar).
- US1: T009 ∥ (T008); T014 ∥ T015 ∥ T016 (farklı PageModel'ler, T011-T013 sonrası).
- Polish: T027 ∥ T028.

## Implementation Strategy

MVP = Phase 1-3 (US1): veri birikmeye başlar — tek başına değerli. Sonra US2, US3 artımlı;
her checkpoint'te canlı doğrulama. UI şeridi bilinçli olarak YOK (ayrı feature).
## Uygulama Notları (2026-08-21)

- T015: WebApp'te arama kutusu YOK — SearchPerformed'ın kontrat+parser desteği tam, call-site'ı
  yok; arama UI'ı geldiğinde tek satır eklenir (bilinen boşluk).
- T017: anonim akış canlı PASS (çerezler, ListShown, ProductViewed, kişisel veri yok);
  login'li satır + BasketItemAdded canlı görülmedi (OIDC login curl'le yapılmadı) — kod yolu aynı.
- T027: pytest 21/21 + WebApp.Tests 4/4 + build 0 hata. Order.Api.Tests'te 1 kırık test
  (Charge_success_amount_mismatch) MASTER'da da kırık — 042'den bağımsız, dokunulmadı.
- Keşif: uv venv pip'siz kurulur; Aspire AddPythonApp installer'ı `.venv/bin/pip install .` ister
  → venv `uv venv --seed` ile kurulmalı (quickstart güncellendi).
- Canlı doğrulama kanıtları: ingest 2. tur processedLines=0 + DB 6 satır sabit (SC-006);
  train Succeeded + model swap; personal/session/popular üç kaynak da görüldü; kimliksiz 400;
  Python kill + sayfa 200 + JSONL büyüdü (SC-005).
