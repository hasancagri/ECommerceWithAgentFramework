# Feature Specification: File Storage Registry (File.Api DB'li BC)

**Feature Branch**: `082-file-storage-registry` · **Created**: 2026-09-22 · **Status**: Draft

**Input**: File.Api'yi DB'siz proxy'den **DB'li gerçek BC**'ye çevir. Dosya (bugün kapak görseli) bir
**kayıt defterinde** tutulur; her mantıksal dosya birden çok fiziki depoda (R2 + AWS S3 + yerel disk +
ileride B2/Cloudflare Images) **aynı anda** yaşayabilir. Uygulamanın tek dosya yazma + URL-çözümleme
kapısı; diğer servisler depolama şemasını/provider'ı bilmez, hiçbir zaman doğrudan Cloudflare'e gitmez.

## Ölçek

**Tam** — yeni aggregate + child entity + yeni DB (`fileDb`, Marten) + servisler-arası çözümleme kontratı.

## Clarifications (2026-09-22) — kararlar kilit

- **Kayıt modeli:** `FileAsset` (aggregate) + `FileStorageLocation` (child entity, List). Bir dosya = bir
  `FileAsset`; her fiziki kopya = bir `FileStorageLocation` (`StorageType` + `StorageFilePath`).
- **Provider-agnostik:** `StorageType` enum (Local, R2, S3, CloudflareImages, B2). `StorageFilePath` = o
  backend'in dosyayı bulmak için ihtiyaç duyduğu key/path (R2'de ISBN, opaque provider'da opaque ID). Full
  URL veriye GÖMÜLMEZ — URL, `StorageType`+config ile çözümleme anında üretilir.
- **Çözümleme yerel:** URL sorgusu File.Api'nin KENDİ `fileDb`'sinden karşılanır — dış depoya (Cloudflare)
  gidilmez (20k dış çağrı yok).
- **Yazma senkron:** kapak yazımı seyrek; async/404 yarışı yok.
- **081 geçersiz:** File.Api "DB'siz destek servisi" kararı bu spec'le emekli (meşru evrim). IFileStore
  backend soyutlaması korunur.

## User Scenarios & Testing *(mandatory)*

### US1 - Dosya kaydı + çoklu-konum (P1) 🎯 MVP

Bir dosya bir backend'e yazılır ve kayıt defterine işlenir; aynı dosya ikinci bir backend'e de yazılınca
tek `FileAsset` altına ikinci konum eklenir.

**Independent Test**: `FileAsset` oluştur (ImageName + ilk konum) → ikinci `StorageType` konumu ekle →
`Locations` iki kayıt gösterir; aynı `StorageType` tekrar eklenince yeni satır oluşmaz (upsert).

**Acceptance**:
1. **Given** kayıtsız ImageName, **When** {R2, path} ile kayıt, **Then** tek konumlu `FileAsset` oluşur.
2. **Given** {R2} konumlu FileAsset, **When** {S3, path} eklenir, **Then** iki konumlu olur.
3. **Given** {R2} konumlu FileAsset, **When** {R2, yeni-path} eklenir, **Then** ikinci R2 satırı OLUŞMAZ
   (aynı StorageType tek — upsert/red).
4. **Given** konumsuz kayıt denemesi, **When** oluştur, **Then** reddedilir (en az bir konum şart).

### US2 - URL çözümleme, depoya gitmeden (P1)

Bir servis (ör. Catalog) ImageName listesiyle URL ister; File.Api kendi DB'sinden çözer, dış depoya gitmez.

**Independent Test**: N ImageName ver → tek sorguyla URL'ler döner; hiçbir dış-depo çağrısı yapılmaz.

**Acceptance**:
1. **Given** kayıtlı ImageName, **When** URL sorulur, **Then** tercih edilen konumdan üretilmiş URL döner.
2. **Given** kayıtsız ImageName, **When** URL sorulur, **Then** "yok" (URL üretilmez, hata değil).
3. **Given** N ImageName (batch), **When** sorulur, **Then** tek turda çözülür (ImageName başına dış çağrı yok).

### US3 - Provider bağımsızlığı / geçiş (P2)

Depo aracı değişince (R2 → başka) sadece kayıt defteri güncellenir; tüketici servisler dokunulmaz.

**Independent Test**: Bir FileAsset'e yeni `StorageType` konumu ekle → çözümlemenin ürettiği URL yeni
provider'a dönebilir; ImageName ve tüketicideki referans değişmez.

**Acceptance**:
1. **Given** {R2} konumlu dosya, **When** {B2} konumu eklenip tercih B2 yapılır, **Then** çözümleme B2 URL'i
   üretir; ImageName sabit.
2. **Given** bir provider kaybı, **When** kayıt sorgulanır, **Then** dosyanın hangi provider'larda kopyası
   olduğu görülür (redundancy görünürlüğü).

### US4 - Mevcut kapakların kayıt defterine alınması (P2)

R2'deki 19709 kapak (ImageName=ISBN) bir kez `fileDb`'ye `{R2, ISBN}` konumuyla işlenir.

**Independent Test**: Backfill çalışır → her ISBN için tek konumlu `FileAsset`; tekrar çalışınca yinelenmez.

**Acceptance**:
1. **Given** R2'de kapaklar + boş fileDb, **When** backfill, **Then** her ISBN için `{R2, ISBN}` FileAsset yazılır.
2. **Given** dolu fileDb, **When** backfill re-run, **Then** var olanlar atlanır (idempotent).

### Edge Cases

- Aynı `StorageType`'tan ikinci konum → yeni satır yok (upsert/red, invariant 1).
- Konumsuz `FileAsset` → oluşturulamaz; son konumun silinmesi → ya reddedilir ya FileAsset silinir (invariant 2).
- ImageName değişikliği → yasak (immutable, invariant 3); ImageName tekil.
- Çözümlemede birden çok konum → tercih sırası (ör. config'li öncelik) belirler hangi URL üretilir.
- Kayıtsız ImageName sorgusu → "yok", hata değil (US2.2).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Sistem her mantıksal dosyayı bir `FileAsset` (ImageName + metadata: ContentType, SizeBytes,
  CreatedAt) olarak kalıcı saklamalı; ImageName **tekil**.
- **FR-002**: `FileAsset` bir veya daha çok `FileStorageLocation` (StorageType + StorageFilePath) taşımalı;
  koleksiyon yalnız aggregate davranışıyla değişmeli.
- **FR-003 (invariant 1)**: Bir `FileAsset`'te aynı `StorageType`'tan **en fazla bir** konum olmalı
  (provider başına tek kopya; ikinci ekleme upsert ya da reddedilir).
- **FR-004 (invariant 2)**: `FileAsset` **en az bir** konumla var olmalı — konumsuz oluşturulamaz; son
  konum kaldırılamaz (ya reddedilir ya `FileAsset` silinir).
- **FR-005 (invariant 3)**: ImageName oluşturulduktan sonra **değişmez**.
- **FR-006**: Sistem bir dosyayı hedef backend'e yazıp ilgili `StorageType` konumunu kayıt defterine
  eklemeli (senkron; async değil).
- **FR-007**: Sistem verilen ImageName(ler) için URL'i **kendi kayıt defterinden** çözmeli — dış depoya
  çağrı YAPMADAN; batch destekli (ImageName başına dış çağrı yok).
- **FR-008**: URL provider-agnostik üretilmeli: `StorageType` + config-taban'dan; **full URL veride
  saklanmaz** (StorageFilePath = key/path ya da opaque ID).
- **FR-009**: Kayıtsız ImageName sorgusu "yok" dönmeli (hata değil).
- **FR-010**: Sistem bir dosyanın hangi provider'larda kopyası olduğunu sorgulanabilir kılmalı (redundancy
  görünürlüğü + anında durum).
- **FR-011**: Mevcut R2 kapakları (ImageName=ISBN) bir kez kayıt defterine alınmalı (idempotent backfill).

### Key Entities

- **FileAsset** (aggregate root): ImageName (mantıksal anahtar, kapakta ISBN — tekil, değişmez),
  ContentType, SizeBytes, CreatedAt; konumlar koleksiyonu (private, okuma salt-okur).
- **FileStorageLocation** (child entity): Id, StorageType (Local/R2/S3/CloudflareImages/B2), StorageFilePath
  (backend key/path ya da opaque ID), CreatedAt. Kendi yaşam döngüsü (eklenir/kaldırılır).

## Success Criteria *(mandatory)*

- **SC-001**: URL çözümleme bir dosya için **0 dış-depo çağrısı** yapar (yalnız kayıt defteri).
- **SC-002**: N dosyanın URL'i **tek turda** (batch) çözülür; süre N ile lineer değil, tek-sorgu ölçeğinde.
- **SC-003**: Bir dosya ≥2 provider'da kopyalanabilir ve her kopyanın yeri sorgudan görülebilir.
- **SC-004**: Depo aracı değişince tüketici servislerde **0 değişiklik** (ImageName sabit; URL File.Api'den).
- **SC-005**: 19709 mevcut kapak kayıt defterine alınır; backfill re-run 0 yinelenen üretir.
- **SC-006**: Aynı StorageType'tan ikinci konum / konumsuz FileAsset / ImageName değişikliği **her zaman
  reddedilir** (invariant'lar).

## Assumptions

- File.Api DB'li BC olur (kendi `fileDb`'si; BC izolasyonu korunur). 081'in "DB'siz" kararı emekli.
- IFileStore backend soyutlaması korunur (LocalDiskFileStore + S3FileStore→R2). Yazma yolu bu arayüzü kullanır.
- Serve endpoint (`GET /files/v1/covers/{isbn}`) kalır; prod okuma CDN/R2 URL'inden (Desen B) — File.Api sıcak
  read yolunda değil.
- ImageName kapaklarda ISBN = Product.Gtin (ürün başına tekil, uygulama düzeyinde).
- Çözümlemede çoklu konum varsa tercih config'li öncelikle seçilir (ör. R2 önce).

## Dependencies

- R2 (bugünkü asıl backend) + mevcut 19709 kapak (081 sonucu).
- Marten/Postgres (`fileDb`) — proje standardı.

## Kapsam dışı

- Gerçek offsite backup (B2 mirror/replication) — backlog (durability faslı).
- `Product.ImageUrl` rewrite — Catalog Excel import feature'ı.
- Cloudflare Images'e geçiş (opaque URL) — model destekler ama bu spec'te yapılmaz.
- Upload yönetim UI / dış yazma yüzeyi.