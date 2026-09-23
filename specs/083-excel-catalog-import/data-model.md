# Data Model: Excel Katalog Import

## ImportRow (YENİ — Catalog, `Import/ImportRow.cs`, Marten doc, aggregate DEĞİL)

Excel'den okunan ham katalog kaydı + işleme durumu. Import makinesinin geçici defteri.

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | Marten kimliği |
| ImportSessionId | Guid | Hangi yükleme oturumundan (izlenebilirlik) |
| Isbn | string | idempotency anahtarı (satır sırası/tekillik) |
| Title, Authors, Publisher | string | authors `;` ayraçlı çoklu-yazar |
| PriceTry | decimal | yayın kapısı (publish_imported fiyat>0) |
| Stock | int | InitialStock (ProductAdded) |
| CategoryMid, CategoryLeaf | string | taksonomi (leaf==mid tuzağı — bkz OpenLibrary notu) |
| Tags | string | `;` ayraçlı |
| Specs | string | `k=v; k=v` (Dil/Sayfa/Cilt/Yıl) |
| Description | string | ürün açıklaması |
| FamilyCode | string? | varyant grubu |
| Status | enum | Pending / Processed / Failed |
| Error | string? | Failed sebebi (raporlama FR-010) |
| ProductId | Guid? | işlenince dolar; publish_imported Gtin sorgusu olmadan ürünü bulur |
| CreatedAt | DateTimeOffset | |

- **Not kullanılan kolonlar:** `imageUrl` (kapak async File.Api'den), `discount` (v1 dışı).
- **Durum geçişi (domain-TDD):** Pending → Processed (ürün oluştu/atlandı) | Pending → Failed (zorunlu alan eksik/parse hata).
  Processed/Failed terminal. Geçiş metotları saf → test-first.
- **Idempotency:** işleme anında ISBN üründe VARSA ürün oluşturulmaz ama satır yine Processed (additive-only atla).

## ImportSession (YENİ — Catalog, `Import/ImportSession.cs`, Marten doc; 078 CredentialEntrySession ikizi)

Yükleme ekranını yetkilendiren kısa-ömürlü tek-kullanımlık capability token.

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | |
| Token | string | 256-bit base64url (unique index) |
| ExpiresAt | DateTimeOffset | LinkLifetime (config, ör. 60 dk) |
| ConsumedAt | DateTimeOffset? | POST başarılı yükleme = consume (one-time) |
| RowCount | int? | yükleme sonrası alınan satır sayısı |
| CreatedAt | DateTimeOffset | |

- `IsUsable(now)` = `ConsumedAt is null && now <= ExpiresAt`. Token URL dışında taşınmaz; ekran dosya-yazma-only.

## Product (MEVCUT — değişmez, `Domains/Products/Product.cs`)

Import bağlamında kullanılan mevcut yüzey:
- **Doğum:** import → TASLAK (`Published=false`). ImageUrl boş.
- `SetImage(url)` — CoverIngested tüketiminde çağrılır.
- `Publish()` — kapı `Price.Amount > 0`; publish_imported çağırır.
- **İmport-köken izi:** ürünün import'tan mı elle mi geldiğini ayırmak gerekir (publish_imported yalnız
  import-kökenli taslakları alsın). Karar: `ImportRow.ProductId` (işlenince dolar) üzerinden doğrudan
  eşleme — publish_imported `Processed` satırların ProductId'leriyle ürünleri çeker. Product'a yeni alan
  EKLENMEZ (İLKE II, additive minimal); köken izi ImportRow'da durur.

## CoverIngested (YENİ — `Shared/IntegrationEvents.cs`)

```
record CoverIngested(string Isbn, string Url)
```
- Yayıcı: File.Api (kapak R2'de hazır + registry upsert sonrası). Url = r2.dev public (CoverUrlResolver).
- Tüketici: Catalog `FileConsumers` → ISBN'den ürün → `SetImage` → `ProductChangedEvent`.

## Değişmez event (MEVCUT, yeniden kullanım)

- `ProductAdded(string Barcode, Guid ProductId, int InitialStock)` — Barcode=ISBN. Import processor yayar;
  Stock (mevcut) + File.Api (yeni) tüketir.
- `ProductChangedEvent(...)` — SetImage sonrası Catalog yayar → Storefront read-model günceller.