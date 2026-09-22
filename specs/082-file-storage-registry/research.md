# Phase 0 Research: File Storage Registry

Kararlar sohbet tasarımında (2026-09-22) kilitlendi; burada gerekçe + reddedilen alternatif.

## Karar 1 — Kayıt defteri: Marten `fileDb` + FileAsset aggregate

- **Karar**: File.Api DB'li BC olur; `FileAsset` (aggregate) + nested `FileStorageLocation` (entity) Marten
  dokümanı olarak `fileDb`'de. ImageName unique index.
- **Gerekçe**: URL çözümlemeyi dosya başına 0 dış-çağrıyla + tek batch sorguyla yapmak, çoklu-provider
  redundancy'yi izlemek, tekillik/invariant zorlamak DURUM gerektirir. DB'siz proxy taşıyamaz.
- **Alternatif red**: (a) DB'siz + her çözümlemede depoya sor → 20k dış çağrı, redundancy görünmez.
  (b) Deterministik `{base}/{isbn}` türet (DB yok) → provider'a/şemaya kilitler, opaque URL'i (Cloudflare
  Images) desteklemez, "elimde ne var" anında sorgulanamaz.

## Karar 2 — Fiziki yazma `IFileStore` ardında (082'de yazıldı)

- **Karar**: Bitler `IFileStore` backend'inde (LocalDiskFileStore / S3FileStore→R2). DB **byte tutmaz**,
  yalnız kayıt (ImageName + konumlar + metadata).
- **Gerekçe**: Depo değişimi (R2→B2) fiziki katmanda; kayıt defteri backend-agnostik kalır. 082 dalındaki
  S3FileStore yeniden kullanılır.
- **Alternatif red**: byte'ı DB'ye koymak (Marten) — Postgres'i BLOB deposu yapmak; 780MB+ şişme, yanlış araç.

## Karar 3 — URL provider-agnostik: StorageFilePath = key/path

- **Karar**: `StorageFilePath` = backend'in dosyayı bulmak için ihtiyaç duyduğu key/path (R2'de ISBN, opaque
  provider'da opaque ID). URL çözümlemede `StorageType`'a göre config-base ile üretilir (`CoverUrlResolver`).
- **Gerekçe**: Full URL veriye gömülürse base/provider değişince tüm satır rewrite. Key sakla → base config'te
  → repoint tek yerden.
- **Alternatif red**: full URL saklamak (basit ama repoint pahalı). Not: opaque provider'da StorageFilePath
  zaten opaque ID/URL olur → model ikisini de taşır.

## Karar 4 — Çözümleme kanalı: internal S2S REST (bu spec'te endpoint; tüketici sonra)

- **Karar**: `POST /internal/files/resolve` (batch ImageName → URL) internal S2S. Bu spec'te **endpoint +
  kontrat** tanımlanır; Catalog tüketimi (Product.ImageUrl rewrite) **kapsam dışı** (sonraki Excel import).
- **Gerekçe**: Yapısal (LLM'siz) BC-arası lookup → İLKE I RPC (gRPC/HTTP) izinli. File.Api HTTP-native +
  çözümleme batch/hot-path-değil → REST yeterli ve basit.
- **Alternatif red**: gRPC (konvansiyon "tercih") — kontrat + proto + Catalog client ek iş; batch/seyrek
  lookup için REST daha az sürtünme. gRPC'ye ileride terfi açık (kontrat aynı kalır).

## Karar 5 — Backfill: idempotent hosted service

- **Karar**: `RegistryBackfillHostedService` (config-gated) mevcut kapakları (R2 ve/veya yerel disk) tarar,
  her ISBN için `FileAsset{ImageName=isbn}` + ilgili `{StorageType, isbn}` konumunu upsert eder.
- **Gerekçe**: 19709 kapak zaten fiziki var; kayıt defterini bir kez doldur. İdempotent (varsa atla/merge) →
  reset/re-run güvenli. 082'deki R2SyncHostedService deseni.
- **Alternatif red**: elle script — kontrat/DI çoğaltır; hosted service BC içinde, Marten oturumuna doğrudan erişir.

## Karar 6 — Yazma senkron

- **Karar**: RegisterFile = fiziki yaz (IFileStore) → FileAsset upsert + konum ekle, **senkron**, dönüşte URL.
- **Gerekçe**: Kapak yazımı seyrek (admin/tek-sefer); senkron = URL anında geçerli, 404 yarışı yok, basit.
- **Alternatif red**: async offload — düşük yazma-latency getirisi kapakta önemsiz; 404/swap karmaşası → YAGNI.