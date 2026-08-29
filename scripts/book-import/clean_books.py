#!/usr/bin/env python3
"""books.json sadeleştirme (yerinde) — 052-görsel veri temizliği.

1) Harf ile başlamayan yazarları at (rakam/sembol/tırnakla başlayan: "3dtotal", "-", '"Family Circle"'...).
   Kitabın tüm yazarları harf-dışıysa kitap SİLİNİR. Aksi halde yalnız harf-dışı ortak-yazar düşer.
2) Tarih-tipi kategori leaf'lerini (yıl/yüzyıl/savaş-tarihi/"nyt:...=YYYY-MM-DD") kategoriMid'e KATLA
   → çok sayıda tarih-çöpü leaf teke iner (kendi üst kategorisine).

Kullanım:
  BOOKS=src/services/catalog/Catalog.Api/Seeding/Data/books.json
  python3 scripts/book-import/clean_books.py "$BOOKS"
"""

import collections
import json
import re
import sys

_DATE_RE = re.compile(r"\b(1[0-9]{3}|20[0-9]{2})\b|centur|\bcent\.|[0-9]{4}\s*-\s*[0-9]{4}", re.I)
_TRAIL_PAREN = re.compile(r"\s*\([^)]*\)\s*$")  # sondaki "(açıklama)"

# Az kitaplı leaf'ler üst kategoriye (mid) katlanır — uzun kuyruk çöpü teke iner.
MIN_LEAF_BOOKS = 30


def _starts_with_letter(name):
    return bool(name) and name[:1].isalpha()  # Unicode: aksanlı harf de harftir


def _clean_leaf(leaf, mid):
    """Tarih-tipi → mid; sondaki (…) kırp → doğal birleşme ("Poetry (…)" = "Poetry"); boş → mid."""
    if _DATE_RE.search(leaf):
        return mid
    leaf = _TRAIL_PAREN.sub("", leaf).strip()
    return leaf or mid


def clean(records):
    # 1) Yazar temizliği + leaf ad temizliği (tarih→mid, paren kırp).
    kept = []
    dropped_books = 0
    stripped_authors = 0
    for rec in records:
        authors = [a for a in rec.get("authors", []) if _starts_with_letter(a)]
        stripped_authors += len(rec.get("authors", [])) - len(authors)
        if not authors:
            dropped_books += 1
            continue
        rec["authors"] = authors
        rec["categoryLeaf"] = _clean_leaf(rec.get("categoryLeaf", ""), rec.get("categoryMid", ""))
        kept.append(rec)

    # 2) Az kitaplı leaf'i mid'e katla (ortak isme topla).
    counts = collections.Counter(r["categoryLeaf"] for r in kept)
    folded_tail = 0
    for r in kept:
        if counts[r["categoryLeaf"]] < MIN_LEAF_BOOKS and r["categoryLeaf"] != r.get("categoryMid"):
            r["categoryLeaf"] = r.get("categoryMid", r["categoryLeaf"])
            folded_tail += 1

    return kept, dropped_books, stripped_authors, folded_tail


def main():
    if len(sys.argv) != 2:
        print("usage: clean_books.py <books.json>", file=sys.stderr)
        sys.exit(2)

    path = sys.argv[1]
    with open(path, encoding="utf-8") as f:
        records = json.load(f)

    cleaned, dropped, stripped, folded = clean(records)

    with open(path, "w", encoding="utf-8") as f:
        json.dump(cleaned, f, ensure_ascii=False, indent=2)

    distinct_leaf = len({r["categoryLeaf"] for r in cleaned})
    print(f"# {len(records)} → {len(cleaned)} kitap "
          f"(silinen {dropped}, düşen harf-dışı yazar {stripped}, uzun-kuyruk leaf mid'e katlandı {folded}) "
          f"| distinct leaf → {distinct_leaf}",
          file=sys.stderr)


if __name__ == "__main__":
    main()
