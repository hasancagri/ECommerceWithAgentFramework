# Phase 1 Data Model: Product Enrichment Agent

İki bounded context'in modeli. Agent'ın kendi kalıcı modeli yoktur (stateless worker).

## Product (Catalog BC — mevcut aggregate, genişletilir)

`Catalog.Api.Domains.Products.Product : AggregateRoot`

**Bu feature için ilgili alanlar**

| Alan | Tip | Not |
|------|-----|-----|
| Name | string | Üretim için bağlam (girdi) |
| Brand | BrandType | Üretim için bağlam (girdi) |
| Description | string | Agent'ın doldurduğu eksik alan; **≤100 karakter** (AI garanti eder), bir kez yazılınca readonly |
| ImageUrl | string? | Agent'ın doldurduğu eksik alan (File URL'i) |
| IsComplete | bool | Description+ImageUrl dolu mu; aggregate'te türetilir |
| IsActive | bool | Admin aktifliği (mevcut) |
| IsOnSale | bool | `IsActive && IsComplete`; saklanmaz |

**Yeni davranış metotları (aggregate içinde, idempotent)**

- `SetDescriptionIfEmpty(string description) : ResultDomain`
  - Description zaten doluysa "atlandı" (yeni resource kodu) döner, değiştirmez (FR-005).
  - Boşsa yazar, `RecalculateCompleteness()` çağırır.
- `SetImageUrlIfEmpty(string imageUrl) : ResultDomain`
  - ImageUrl zaten doluysa "atlandı" döner (FR-005). Boşsa yazar + recalculate.

> Mevcut `UpdateImageUrl` koşulsuz yazar; enrichment yolu üzerine-yazmayı önlemek için
> yeni "IfEmpty" metotlarını kullanır. `RecalculateCompleteness` değişmez.

**Invariant**: IsComplete daima (Description dolu ∧ ImageUrl dolu). Kısmi üretim ürünü
satışa çıkaramaz (FR-006) — çünkü tek alan doldurulunca IsComplete false kalır.

## Görsel saklama (File BC — dosya-sistemi tabanlı; YENİ aggregate/DB YOK)

File.Api görseli **yalnızca dosya sistemine** yazar; bu feature için yeni aggregate ya da
Marten persistence **eklenmez**. Dosya adı ProductId'den deterministik türetilir.

| Öğe | Değer |
|-----|-------|
| Dosya yolu | `Images/{ProductId}.png` (deterministik) |
| ContentType | `image/png` (sabit) |
| Boyut | **256×256 px** (servis edilen) — üretim 1024, File.Api küçültür |
| PublicUrl | `/images/{ProductId}.png` — statik serve; hesaplanır, saklanmaz |

**Davranış (upload handler — `IDocumentSession` yok)**

- Gelen byte'ları **256×256'ya küçültüp** `Images/{ProductId}.png`'e yazar; klasör yoksa
  oluşturur. Resize için File.Api'ye bir görsel kütüphanesi gerekir (ör. SixLabors.ImageSharp).
- **Idempotency**: dosya zaten varsa üzerine yazmaz/atlar, mevcut deterministik URL'i döner
  (DB sorgusu değil, **dosya varlık kontrolü**). Catalog tarafı "ImageUrl doluysa atla".
- Deterministik URL'i döner; Catalog `ImageUrl`'e bu yazılır.

**Neden DB yok**: Depo = dosya sistemi, idempotency = varlık kontrolü, URL = deterministik
→ Marten/FileAsset fonksiyonel gereksinim değil. Audit/çoklu-görsel gerekirse tekrar bakılır.

> **Legacy not (ayrı iş)**: File.Api'de bugün Marten + `fileDb` + course-picture event akışı
> bağlı. Bu feature ona **dokunmaz**; sökümü ayrı/opsiyonel bir cleanup'tır.

## Enrichment akışı (kalıcı olmayan iş nesnesi)

Agent, ürün başına şu geçici sonucu üretir (DB'ye yazılmaz, log/rapor için):

| Alan | Değer |
|------|-------|
| ProductId | Guid |
| DescriptionResult | Ok / Skipped / Failed |
| ImageResult | Ok / Skipped / Failed |

Toplu koşu bu satırların listesini raporlar (FR-007): ürün başına başarı/atlandı/hata.