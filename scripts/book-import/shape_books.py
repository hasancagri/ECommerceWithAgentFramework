#!/usr/bin/env python3
"""İş1 — build-time kitap şekillendirme (051-book-import).

Ham Amazon "popular books" dataset'ini (≈20MB, ISBN'li + ASIN-only karışık) süzer ve Catalog
açılış seeder'ının okuduğu küçük `books.json`'u üretir. Ham dosya repoya GİRMEZ; yalnız çıktı commit'lenir.

Kurallar (research.md D1/D6/D7/D8):
  - Yalnız ISBN10 dolu kayıtlar (dijital/ASIN-only düşer). ISBN ile dedup (ilk kayıt kazanır).
  - brand verbatim (opsiyonel "by " öneki kırpılır); boşsa "Unknown"a bağlanır (kitap atılmaz).
  - priceTry = (final_price ?? initial_price) * SABIT_KUR (USD→TL); ikisi de boşsa null (taslak kalır).
  - imageUrl = image_url (boşsa null → vitrin placeholder).
  - categories: "Books" atılır; [1]=mid (parent), [2]=leaf (primary). 4-derin kayıtta da [1]/[2] alınır.
  - ATILAN: description, item_weight, product_dimensions, format, rating, reviews_count, asin,
    discount, initial_price(çıktıya değil, fallback'e), seller_name, manufacturer, upc...

Kullanım:
  python3 scripts/book-import/shape_books.py <ham_dataset.json> > \
    src/services/catalog/Catalog.Api/Seeding/Data/books.json
"""

import json
import sys

# Sabit USD→TL kuru (canlı kur yok — kullanıcı kararı D8). Keyfi; sonraki iş (ML fiyat) ayarlar.
FX_USD_TRY = 40.0


def _nonempty(v):
    return v not in (None, "", [], {})


def _clean_brand(raw):
    if not isinstance(raw, str):
        return "Unknown"
    b = raw.strip()
    if b.lower().startswith("by "):
        b = b[3:].strip()
    return b if b else "Unknown"


def _price_try(rec):
    usd = rec.get("final_price")
    if not _nonempty(usd):
        usd = rec.get("initial_price")
    if not _nonempty(usd):
        return None
    try:
        return round(float(usd) * FX_USD_TRY, 2)
    except (TypeError, ValueError):
        return None


def shape(records):
    out = []
    seen = set()
    for rec in records:
        isbn = rec.get("ISBN10")
        if not _nonempty(isbn):
            continue
        if isbn in seen:
            continue
        seen.add(isbn)

        cats = rec.get("categories") or []
        # cats[0] = "Books" sabiti; mid/leaf sonraki iki seviye.
        if len(cats) < 3:
            # Veri profili: leaf-eksik sıfır. Yine de savunmacı — tür çözülemeyeni atla.
            continue

        out.append({
            "isbn": isbn,
            "title": rec.get("title") or "",
            "brand": _clean_brand(rec.get("brand")),
            "priceTry": _price_try(rec),
            "imageUrl": rec.get("image_url") if _nonempty(rec.get("image_url")) else None,
            "categoryMid": cats[1],
            "categoryLeaf": cats[2],
        })
    return out


def main():
    if len(sys.argv) != 2:
        print("usage: shape_books.py <raw_dataset.json>", file=sys.stderr)
        sys.exit(2)

    with open(sys.argv[1], encoding="utf-8") as f:
        records = json.load(f)

    shaped = shape(records)
    json.dump(shaped, sys.stdout, ensure_ascii=False, indent=2)
    print(f"\n# {len(shaped)} kayıt üretildi", file=sys.stderr)


if __name__ == "__main__":
    main()