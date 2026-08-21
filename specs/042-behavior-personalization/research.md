# Research: Davranış-Bazlı Kişiselleştirme (042)

Tasarım oturumu (2026-08-21) kararları + plan aşamasında çözülen teknik bilinmeyenler.

## R1 — JSONL yazıcı: Serilog değil, özel hafif yazıcı

- **Decision**: WebApp'e Serilog EKLENMEZ. `BehaviorLogWriter`: `Channel<BehaviorEvent>` kuyruğu +
  arka plan tüketici, `System.Text.Json` ile satır yazar, günlük dosya (`behavior-YYYYMMDD.jsonl`).
- **Rationale**: Tasarım oturumunda "Serilog kategorisi" mekanizma örneğiydi; öz = ayrı JSONL dosyası.
  Serilog = 2-3 paket + config; özel yazıcı ~60 satır, format tam kontrolde (SchemaVersion, alan
  adları kontratın kendisi). Kullanıcı tercihi: dolaylama yerine düz kod. Channel sayesinde sayfa
  akışı hiç bloklanmaz (FR-008); yazma hatası yutulur + ILogger'a düşer (sayfa asla düşmez).
- **Alternatives considered**: Serilog File sink + kategori filtresi (paket/config yükü, format
  şablon diline bağlanır); doğrudan `File.AppendAllText` (istek yolunda I/O, kilitlenme riski).

## R2 — Aspire Python hosting

- **Decision**: `Aspire.Hosting.Python` **13.3.5** (NuGet'te doğrulandı; AppHost.Sdk ile sürüm-eş —
  DCP dersi). `builder.AddPythonApp("personalization", "../services/personalization", "main.py")` +
  `.WithHttpEndpoint(env: "PORT")` + `.WithReference(personalizationDb)`. Servis klasöründeki `.venv`
  ile koşar; uvicorn `main.py` içinden `PORT` env'ini okuyarak başlar. API `[Experimental]`
  (`ASPIREHOSTINGPYTHON001` pragma ile susturulur) — dev ortamı için kabul.
- **Rationale**: Tüm Aspire.Hosting.* aynı sürümde tutulmazsa DCP çöküyor (bilinen ders).
  Container yerine lokal süreç: dev makinesinde venv + hızlı iterasyon; dosya paylaşımı bedava.
- **Alternatives considered**: Dockerfile + container resource (dosya paylaşımı volume ister,
  iterasyon yavaş); CommunityToolkit uv hosting (ekstra bağımlılık, gerek yok).

## R3 — Paylaşımlı log dizini

- **Decision**: Repo kökünde gitignore'lu `artifacts/behavior-logs/`. AppHost yolu tek yerde kurar,
  WebApp'e `BehaviorLog__Directory` env, Python'a `BEHAVIOR_LOG_DIR` env olarak enjekte eder.
- **Rationale**: Tek makine dev varsayımı (spec Assumption). Yol tek kaynaktan (AppHost) gelir;
  iki taraf asla sabit yol kodlamaz. Options pattern: WebApp'te `BehaviorLogOptions` POCO'ya bağlanır.
- **Alternatives considered**: OS temp (Aspire restart'ında kaybolur, debug zorlaşır); WebApp
  content-root altı (Python'un WebApp klasörüne uzanması sınır ihlali kokusu verir).

## R4 — Idempotent ingest

- **Decision**: Python `ingest_offsets(file_name, byte_offset)` tablosu tutar; her turda offset'ten
  okur, satırları işler, INSERT + offset güncellemesini TEK transaction'da yapar. Emniyet kemeri:
  `behavior_events(source_file, line_no)` UNIQUE — offset kaybolsa da çift satır imkânsız (FR-009,
  SC-006). Bozuk satır: atla + `skipped_lines` sayacıyla logla (FR-010). Rotasyon: dünkü dosya
  değişmez (immutable) — yazıcı yalnız bugünün dosyasına ekler.
- **Rationale**: Şemaya EventId alanı eklemeden (onaylı kontrat korunur) kesin idempotentlik.
- **Alternatives considered**: Satıra EventId (GUID) + UNIQUE (kontratı değiştirirdi); yalnız
  offset (offset kaybında çift kayıt riski); dosyayı işleyince taşı/sil (yazıcıyla yarış riski).

## R5 — Model: implicit ALS + popüler fallback

- **Decision**: `implicit` kütüphanesi `AlternatingLeastSquares` (factors=32, iterations=15);
  girdi: user×item CSR matrisi — kimlik = UserId varsa o, yoksa AnonymousId; ağırlıklar view=1,
  add-to-basket=3 (impression matrise GİRMEZ, yalnız depolanır — spec kapsam kararı). Çıktı `.npz`
  (faktörler + id eşlemeleri) atomik yazılır (tmp + rename); süreç-içi model swap tek referans
  değişimi — sorgu kesintisi yok (FR-011). "En popüler": eğitim job'ı son 7 günün en çok görüntülenen
  top-50 ürününü `popular_products` tablosuna yazar; model/kimlik yoksa fallback (FR-013). Anonim
  kimlik matriste yoksa oturum ürünlerinden `similar_items` toplaması, o da boşsa popüler.
- **Rationale**: Pozitif-yalnız veriyle çalışır; ALS az veride çökmez; oturum-bazlı skorlama
  (matris-dışı ziyaretçi) item-benzerliğiyle çözülür.
- **Alternatives considered**: ML.NET MF (kullanıcı Python'a yöneldi); LightGBM/FFM CTR (impression
  modeli sonraki faz — spec kapsam dışı); sklearn TruncatedSVD (implicit'in API'si işe daha uygun).

## R6 — Zamanlama + dev tetiği

- **Decision**: APScheduler (FastAPI lifespan içinde): ingest 30 sn'de bir, eğitim 10 dk'da bir
  (env ile ayarlanabilir). Dev aracı: `POST /admin/ingest` + `POST /admin/train` anonim tetikler —
  Procurement `POST /v1/feeds/pull` emsali. SC-001 (<1 dk) 30 sn ingest ile sağlanır.
- **Rationale**: Hangfire Python'da yok; APScheduler süreç-içi ve yeterli. Manuel tetik canlı
  doğrulamayı bekletmesiz yapar.
- **Alternatives considered**: OS cron (Aspire yaşam döngüsü dışı); watchdog dosya-izleme
  (karmaşık; 30 sn poll yeter).

## R7 — Anayasa İlke I gerilimi

- **Decision**: Sapma "UI→BC dosya-tabanlı besleme" olarak dar yorumlanır, plan Complexity
  Tracking'de gerekçeli: WebApp bounded context DEĞİLDİR (DB'siz UI/BFF); İlke I "context'ler arası"
  kanalları düzenler. Kalıcı netlik için amendment ÖNERİLİR (ayrı iş, implement'i bloklamaz):
  İlke I'e ek — "Kayıp-toleranslı telemetri/davranış verisi, UI katmanından tek-tüketicili BC'ye
  versiyonlu log dosyasıyla beslenebilir; ikinci tüketici doğarsa kanal integration event'e terfi
  eder." `/speckit-constitution` ile ayrıca işlenir.
- **Rationale**: Kuralı sessizce esnetmek yerine kayıtlı yorum + açık amendment yolu.
- **Alternatives considered**: Şimdi amendment (akışı bloklar, ayrı tartışma hak ediyor);
  integration event'e dönmek (tasarım oturumunda bilinçli reddedildi).

## R8 — Kimlik çerezleri

- **Decision**: `AnonymousIdMiddleware`: kalıcı `pz_aid` çerezi (1 yıl, HttpOnly, ilk ziyarette
  GUID) + oturumluk `pz_sid` çerezi (SessionId). Login'li kullanıcıda UserId claim'den okunur;
  AnonymousId de yazılmaya devam eder (stitching YOK — yalnız aynı satırda iki alan). Çerezler
  kişisel veri taşımaz (rastgele GUID); davranış satırında kişisel alan yok (FR-007).
- **Rationale**: C login modeli; SessionId sunucu session altyapısı gerektirmeden çerezle çözülür.
- **Alternatives considered**: ASP.NET Session (gereksiz state altyapısı); fingerprinting
  (KVKK riski, reddedildi).

## R9 — Python bağımlılık yönetimi

- **Decision**: `pyproject.toml` + `uv` ile `.venv` (`uv venv && uv pip install -e .`); sabit ana
  bağımlılıklar: fastapi, uvicorn, psycopg[binary], implicit, scipy, apscheduler; test: pytest.
- **Rationale**: uv hızlı ve tek araç; pyproject standart — pip'le de kurulabilir (kilitlenme yok).
- **Alternatives considered**: requirements.txt (meta veri yok); poetry (ağır, gerek yok).