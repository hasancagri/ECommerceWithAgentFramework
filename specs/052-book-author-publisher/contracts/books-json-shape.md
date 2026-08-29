# Contract: books.json şeması (İş1 shape_books.py çıktısı — v2)

**Üretici:** `scripts/book-import/shape_books.py` (build-zamanı) · **Tüketici:** `BookImportHostedService` + `ImportBook` command.

**Girdi (v2):** mevcut committed `books.json` (v1, `brand` alanlı) — ham ~20MB dataset GEREKMEZ; `brand` zaten rol-etiketlerini + isimleri taşır. Script v1→v2 in-place dönüşüm yapar (isbn/title/priceTry/imageUrl/categoryMid/Leaf aynen; `brand` → `authors`+`publisher`).

## Şema (v2)

```json
{
  "isbn": "0007350813",
  "title": "Wuthering Heights (Collins Classics)",
  "authors": ["Emily Brontë"],
  "publisher": "Can Yayınları",
  "priceTry": 159.6,
  "imageUrl": "https://...",
  "categoryMid": "Literature & Fiction",
  "categoryLeaf": "Genre Fiction"
}
```

## Değişiklik (v1 → v2)
- **KALKAN:** `brand: string`.
- **GELEN:** `authors: string[]` (≥1; boşsa `["Unknown"]`), `publisher: string` (4 havuzdan). Contributors YOK (YAGNI).

## shape_books.py mantığı
- **Yazar ayrıştırma** (eski `brand` alanından):
  - virgül/`;` böl; her token'dan trailing `(Rol)` çıkar; `& N more`, `by ` at.
  - `(Author)` → authors. Hiç etiket yoksa → tüm string tek yazar.
  - Yazar-dışı roller (`Illustrator|Narrator|Editor|Compiler|...`) → **atılır** (saklanmaz).
  - Hiç `(Author)` yoksa `authors=["Unknown"]`.
- **Yayınevi (uydurma, kararlı):** `POOL[int(md5(isbn).hexdigest,16) % 4]`. `hash()` DEĞİL (salt'lı). Havuz: `["Can Yayınları","İletişim Yayınları","İş Bankası Kültür Yayınları","Yapı Kredi Yayınları"]`.

## ImportBook eşleme değişikliği
- `BookRecord` + `ImportBookCommand`: `string Brand` → `string[] Authors`, `string Publisher`.
- `GetOrCreateBrandAsync` → `GetOrCreateAuthorsAsync` (liste; her ad get-or-create, Id listesi döner) + `GetOrCreatePublisherAsync`.
- `SetBrand` → `SetAuthors(ids)` + `SetPublisher(id)`.