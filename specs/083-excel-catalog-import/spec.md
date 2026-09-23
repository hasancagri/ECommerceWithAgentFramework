# Feature Specification: Excel Katalog Import

**Feature Branch**: `083-excel-catalog-import`
**Created**: 2026-09-22
**Status**: Draft
**Input**: Admin 19.711 satırlık xlsx'i yükler; ürünler TASLAK oluşur; kapaklar async File.Api'den düşer; ayrı bulk tool ile yayınlanır.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Kataloğu xlsx'ten yükle (Priority: P1)

Admin, mağazanın kitap kataloğunu (binlerce satır) tek bir Excel dosyasından mağazaya alır. Dosya
MCP sohbetine sığmayacak kadar büyük olduğundan, admin bir tool çağırır, dönen güvenli linkten
tarayıcıda dosyayı yükler, sistem satırları arka planda ürünlere dönüştürür.

**Why this priority**: Import olmadan feature yok — mağazayı dolduran çekirdek. Tek başına test edilir.

**Independent Test**: `import_catalog` çağır → dönen linkten xlsx yükle → tüm satırların TASLAK ürün
olarak oluştuğu, tekrar import'ta yeni satır eklenmediği doğrulanır.

**Acceptance Scenarios**:

1. **Given** 19.711 satırlık geçerli xlsx, **When** admin linkten yükler, **Then** her benzersiz ISBN için bir TASLAK ürün oluşur ve durum "işlendi" olur.
2. **Given** import bittikten sonra aynı/örtüşen dosya yeniden yüklenir, **When** işlenir, **Then** var olan ISBN'ler ATLANIR, yalnız yeni satırlar ürün olur (additive-only).
3. **Given** işleme sırasında sistem çöker/yeniden başlar, **When** ayağa kalkar, **Then** yarım kalan satırlar kaldığı yerden işlenir, hiçbir ISBN iki kez ürün olmaz.

---

### User Story 2 - Kapaklar otomatik düşsün (Priority: P2)

Ürün oluştuğunda kapak görseli, admin bir şey yapmadan arka planda ürüne iliştirilir. Kapağı olmayan
kitap yer-tutucuyla kalır; import bunun için beklemez.

**Why this priority**: Görselsiz katalog satılabilir ama yavan; kapak değer katar, ama import'u bloklamaz.

**Independent Test**: Bir ürün import et → kapak deposunda görseli olan ürünün kısa süre içinde
görsele kavuştuğu, olmayanın yer-tutucuda kaldığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** kapak deposunda görseli olan bir ISBN, **When** ürün import edilir, **Then** kısa süre içinde ürün o görsele kavuşur (import tamamlanmasını beklemeden).
2. **Given** kapağı olmayan bir ISBN, **When** ürün import edilir, **Then** ürün yer-tutucuyla kalır, hata üretilmez.

---

### User Story 3 - İçe alınanları toplu yayınla (Priority: P2)

Admin önce tüm kataloğu taslak olarak alır, gözden geçirir, sonra tek komutla satışa uygun olanları
canlıya çıkarır.

**Why this priority**: "Önce yüklensin bakayım, sonra yayınlarım" — admin kontrol noktası. Taslak-doğar
doktrini (mevcut) korunur.

**Independent Test**: Import'tan sonra `publish_imported` çağır → fiyatı olan import-kökenli taslakların
canlı, fiyatsızların taslak kaldığı doğrulanır.

**Acceptance Scenarios**:

1. **Given** import edilmiş taslak ürünler, **When** admin `publish_imported` çağırır, **Then** fiyatı > 0 olan import-kökenli taslaklar yayınlanır (vitrinde görünür).
2. **Given** fiyatı olmayan import taslakları, **When** `publish_imported` çalışır, **Then** bunlar taslak kalır (satılabilir değil).
3. **Given** admin elle oluşturduğu (import-kökenli olmayan) taslaklar, **When** `publish_imported` çalışır, **Then** bunlara dokunulmaz.

---

### Edge Cases

- **Bozuk/eksik satır**: zorunlu alanı (ISBN) olmayan satır "Başarısız" işaretlenir, hata sebebi saklanır, kalan satırlar etkilenmez.
- **Bozuk dosya (xlsx değil / kolon şeması tutmuyor)**: yükleme reddedilir, admin'e neden bildirilir, hiçbir satır alınmaz.
- **Süresi geçmiş/yanlış upload linki**: yükleme reddedilir.
- **Import sürerken tekrar `import_catalog`**: yeni bir upload oturumu açılır; işleme kuyruğu ISBN idempotency ile korunur.
- **Kapak deposu geçici erişilemez**: ürün yine oluşur (taslak), kapak sonradan düşebilir; import bloklanmaz.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Admin, korumalı yönetim yüzeyinden bir import başlatabilmeli; sistem, dosyanın karşıya yükleneceği güvenli, süreli, tek-amaçlı bir yükleme adresi vermeli (büyük dosya sohbet kanalına sığmaz).
- **FR-002**: Sistem yüklenen Excel dosyasını yalnızca bir kez okuyup ham satırları bir bekleme alanına (staging) almalı; sonraki işleme Excel'i yeniden okumamalı.
- **FR-003**: Sistem bekleyen satırları arka planda işlemeli; her satır için benzersiz ISBN başına en fazla bir ürün oluşturmalı (ISBN = tekillik anahtarı).
- **FR-004**: Ürün oluşturma ile satırın "işlendi" işaretlenmesi tek atomik adımda olmalı; çökme sonrası ne çift ürün ne kayıp satır oluşmalı (exactly-once).
- **FR-005**: Import additive-only olmalı: var olan ISBN ATLANIR; mevcut ürün güncellenmez, silinmez, değişiklik algılanmaz.
- **FR-006**: Import ile oluşan her ürün TASLAK doğmalı (yayınlanmamış); vitrinde görünmemeli.
- **FR-007**: Ürün oluştuğunda sistem, kapak deposunun o ürünün görselini bulup iliştirmesini tetikleyecek bir olay yaymalı; bu akış asenkron olmalı ve import'u bloklamamalı.
- **FR-008**: Kapak deposunda görsel varsa ürün, kapak hazır olduğunda görsele kavuşmalı ve bu değişiklik vitrin okuma modeline yansımalı; görsel yoksa ürün yer-tutucuda kalmalı.
- **FR-009**: Admin, ayrı bir toplu-yayın komutuyla (`publish_imported`) yalnızca import-kökenli, fiyatı > 0 olan taslakları yayınlayabilmeli; fiyatsız taslaklar ve import-dışı taslaklar etkilenmemeli.
- **FR-010**: Sistem, başarısız satırların sayısını ve sebeplerini admin'in görebileceği şekilde raporlamalı.
- **FR-011**: Eski seed/demo veri giriş yolu (books.json tabanlı hosted seeder ve demo seeder'lar) kaldırılmalı; Excel import tek katalog giriş yolu olmalı.

### Key Entities

- **Import Satırı (ImportRow)**: Excel'den okunan ham katalog kaydı. 14-kolonluk kilitli şema (ISBN dahil) + işleme durumu (Bekliyor/İşlendi/Başarısız) + hata sebebi. Aggregate değil; import makinesinin geçici defteri.
- **Ürün (Product)**: Import'un ürettiği katalog aggregate'i (mevcut). Import bağlamında TASLAK doğar, import-köken izi taşır, sonradan kapak görseli ve yayın durumu kazanır.
- **Kapak İlişkilendirme Olayı**: "ürün oluştu" ve "kapak hazır" iş olayları; Catalog ile kapak deposu arasındaki asenkron köprü.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Admin, 19.711 satırlık kataloğu tek dosya yüklemesiyle mağazaya alabilir; elle satır girişi gerekmez.
- **SC-002**: İçe alınan her benzersiz ISBN mağazada tam olarak bir ürünle temsil edilir (ne kopya ne eksik).
- **SC-003**: Aynı katalog ikinci kez yüklendiğinde yeni ürün oluşmaz (yalnız gerçekten yeni satırlar eklenir).
- **SC-004**: İşleme yarıda kesilip yeniden başlasa bile sonuç, kesintisiz işlemeyle aynı olur (idempotent/çökme-güvenli).
- **SC-005**: Kapak deposunda görseli olan ürünlerin görselleri, import tamamlanmasını beklemeden ürünlere düşer.
- **SC-006**: Admin toplu-yayın komutundan sonra, fiyatlı import ürünleri vitrinde görünür; fiyatsızlar görünmez.

## Assumptions

- Excel şeması sabit ve kilitli (14 kolon, ISBN sütunu zorunlu ve benzersiz kabul edilir); değişken/keşif şema desteklenmez.
- Kapak görselleri kapak deposunda ISBN anahtarıyla erişilebilir; genel dosya yükleme kapsam dışı (kapak deposu kapak-özel kalır).
- Ürün ImageUrl doğrudan kapak deposunun sahibi olduğu public görsel adresidir; mağaza kendi serve ucunu koymaz.
- Ürün düzenleme (fiyat/künye değişimi) mevcut yönetim tool'larıyla yapılır — re-import ile değil.
- Kapak deposunun public erişimi operasyonel olarak açık (feature öncesi koşul).

## Out of Scope

- Var olan ürünleri güncelleme, silme veya Excel'le senkron tutma (delete-sync/değişim algılama).
- Excel dışı formatlar (CSV, API besleme, çoklu-tedarikçi feed).
- Yayın onay iş akışı / kademeli yayın (tek toplu-yayın komutu yeterli).
- Import ilerlemesinin canlı (yüzde) gösterimi — sayım/sonuç raporu yeterli.