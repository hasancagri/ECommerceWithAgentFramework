# Feature Specification: Kapak Görseli Deposu (File.Api)

**Feature Branch**: `081-cover-image-store` · **Created**: 2026-09-21 · **Status**: Draft

**Input**: Kitap kapaklarını dış hotlink yerine mağazanın **kalıcı yerel deposunda** tut; yeni **File.Api**
görseli **ISBN** ile anahtarlayıp stabil URL'den servis eder. Reset'te kaybolmaz — saatlerce yeniden
indirme yok. (Depoyu S3/MinIO'ya taşımak **sonraki spec**; bu feature depolama arayüz ardında.)

## Ölçek

**Tam** — yeni destek servisi (File.Api) + kalıcı yerel depo + bir-kez veri migration'ı.

## Clarifications (2026-09-21)

- **Anahtar = ISBN** (`covers/{isbn}`, uzantısız; content-type ayrı metadata'da). ProductId reddedildi
  (rastgele Guid, indirme anında bilinmiyor).
- **Backend = kalıcı yerel disk** (bind-mount); reset/prune'a rağmen fiziki durur. S3/MinIO sonraki spec —
  serve/migration kontratı backend-bağımsız (arayüz ardında) ki geçiş kontratı değiştirmesin.
- **Migration bir kez, idempotent**: kaynak `{isbn, imageUrl}` listesi; indir → depoya yaz; varsa atla.
- **Kapak okuma anonim** (public vitrin görseli). `Product.ImageUrl` rewrite + Excel import kapsam dışı.

## User Scenarios & Testing *(mandatory)*

### US1 - Kapak stabil mağaza URL'inden servis edilir (P1)

İstemci bir kitabın kapağını mağaza URL'inden ISBN ile çeker; görsel dış siteden değil kendi deposundan gelir.

**Acceptance**:
1. Depoda `covers/{isbn}` varsa → `GET /files/v1/covers/{isbn}` görseli doğru content-type ile döner (kimliksiz).
2. Depoda yoksa → 404 (servis çökmez).

### US2 - Mevcut kapaklar bir-kez depoya alınır (P1)

Operatör migration'ı çalıştırır; kaynak listedeki her kapak indirilip kalıcı depoya yazılır.

**Acceptance**:
1. Kaynak liste + boş depo → migration erişilebilir her görseli `covers/{isbn}` yazar + content-type saklar;
   özet `{yazıldı, atlandı, başarısız}` döner.
2. Bir satırın `imageUrl`'i erişilemez/bozuk → o satır atlanır (başarısız sayılır), süreç devam eder.

### US3 - Reset sonrası yeniden indirme yok / idempotent (P2)

**Acceptance**:
1. Dolu depo yeniden başlatılınca kapaklar yeniden indirilmeden erişilebilir kalır.
2. Migration re-run'ı var olanları yeniden İNDİRMEZ; yalnız eksik ISBN'leri indirir.

### Edge Cases

- Aynı ISBN yeniden yazımı → son görsel üzerine yazar (ürün başına tek kapak).
- `imageUrl` boş → atlanır (kaynak yok). Çok yavaş/büyük indirme → zaman aşımıyla atlanır, süreci kilitlemez.
- Content-type kaynakta yoksa → genel ikili tip (`application/octet-stream`).

## Requirements *(mandatory)*

- **FR-001**: Sistem kapağı **ISBN anahtarıyla** kalıcı depoda saklamalı (`covers/{isbn}`).
- **FR-002**: Sistem verilen ISBN için kapağı stabil URL'den doğru content-type ile servis etmeli; yoksa 404.
- **FR-003**: Depo container/servis yeniden başlatmada içeriği **kaybetmemeli** (fiziki kalıcılık).
- **FR-004**: Migration kaynaktan (isbn, imageUrl) her görseli indirip yazmalı; varsa **atlamalı** (idempotent).
- **FR-005**: İndirilemeyen görsel migration'ı **durdurmamalı**; atlanıp özet raporlanmalı `{yazıldı, atlandı, başarısız}`.
- **FR-006**: Content-type kaynaktan türetilip metadata'da saklanmalı; serve header'ı ondan gelmeli.
- **FR-007**: Kapak okuma **anonim** olmalı. Yazma bu feature'da yalnız **iç startup migration**'ıdır
  (dış yazma/upload yüzeyi kapsam dışı — ileride).
- **FR-008**: Depolama erişimi bir **arayüz ardında** olmalı — backend (yerel disk → ileride S3) serve/migration kontratını değiştirmeden değişebilmeli.

### Key Entities

- **Kapak Object'i**: `{isbn (anahtar), içerik byte'ları, content-type, boyut, eklenme zamanı}`. Ürün künyesi/fiyat TUTMAZ.
- **Migration Kaynak Satırı**: `{isbn, imageUrl}` (bugün catalog-import listesi).

## Success Criteria *(mandatory)*

- **SC-001**: Depo/container yeniden başlatıldıktan sonra kapaklar **0 yeniden indirme** ile erişilebilir.
- **SC-002**: Migration erişilebilir görselleri depoya alır; başarısızlar raporlanır, süreç sonuna kadar tamamlanır.
- **SC-003**: Kapak isteği stabil URL'den **<1 sn** içinde doğru görseli döner.
- **SC-004**: Migration re-run'ı var olanları yeniden indirmez; yalnız eksikleri indirir.

## Assumptions

- File.Api **destek servisi**: kendi domain Postgres DB'si yok; kalıcılık dosya deposunda. BC izolasyonu korunur
  (başka BC DB'sine dokunmaz; gateway/mail-mcp emsali).
- Kaynak `imageUrl`'ler migration anında erişilebilir. ISBN = `Product.Gtin`, ürün başına benzersiz.
- Kapaklar public okuma içeriği (hassas değil).

## Dependencies

- Kalıcı yerel dosya deposu (bind-mount host dizini) — Aspire AppHost içinde.
- Migration kaynak listesi `{isbn, imageUrl}` — bugün `catalog-import.xlsx` (repo dışı, ~/dev/catalog-data).

## Kapsam dışı

- S3/MinIO backend (sonraki spec — FR-008 arayüzünün S3 implementasyonu).
- `Product.ImageUrl`'in File.Api URL'ine çevrilmesi (sonraki Excel import).
- Upload yönetim yüzeyi, görsel resize/varyant/CDN.