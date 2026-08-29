#!/usr/bin/env python3
"""İş1 v2 — build-time kitap künye şekillendirme (052-book-author-publisher).

Girdi = mevcut committed `books.json` (v1, `brand` alanlı). Ham ~20MB Amazon dataset GEREKMEZ;
`brand` zaten rol-etiketli isimleri taşır. Script v1→v2 in-place dönüşüm yapar:
  - `brand` → `authors: string[]` (rol-etiketi temizliği; yazar-dışı rol atılır) + `publisher: string`
    (uydurma, ISBN-kararlı; 4 havuz). Diğer alanlar (isbn/title/priceTry/imageUrl/categoryMid/Leaf) aynen.

Kurallar (research.md D4/D6, contracts/books-json-shape.md):
  - Yazar ayrıştırma (eski `brand`'den): virgül/`;` böl; her token'dan trailing `(Rol)` çıkar;
    `& N more` ve leading `by ` at. `(Author)` (veya "Author" içeren rol) → authors. Hiç etiket
    yoksa tüm token'lar yazar. Yazar-dışı roller (Illustrator/Editor/Compiler/Narrator/Foreword...) atılır.
    Hiç yazar kalmazsa `["Unknown"]`.
  - Yayınevi: `POOL[int(md5(isbn),16) % 4]` — kararlı (aynı ISBN hep aynı), tekrar-üretilebilir.
    Python `hash()` salt'lı → KULLANMA; `hashlib.md5` deterministik.

Kullanım (in-place dönüşüm):
  BOOKS=src/services/catalog/Catalog.Api/Seeding/Data/books.json
  python3 scripts/book-import/shape_books.py "$BOOKS" > "$BOOKS.tmp" && mv "$BOOKS.tmp" "$BOOKS"
"""

import hashlib
import json
import re
import sys

# Yayınevi havuzu (uydurma; 4 sabit ad). ISBN-kararlı atama → aynı kitap hep aynı yayınevi.
PUBLISHER_POOL = [
    "Can Yayınları",
    "İletişim Yayınları",
    "İş Bankası Kültür Yayınları",
    "Yapı Kredi Yayınları",
]

_ROLE_SUFFIX = re.compile(r"\s*\(([^)]*)\)\s*$")  # trailing "(Rol)" — rol metni grup 1
_AND_MORE = re.compile(r"\s*&\s*\d+\s+more\s*$", re.IGNORECASE)  # "& 3 more"


def _split_top_level(raw):
    """Virgül/`;` ile böl ama paren içindeki ayraçları koru (ör. "(Author, Illustrator)" tek kalır)."""
    tokens = []
    depth = 0
    buf = []
    for ch in raw:
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth = max(0, depth - 1)
        if ch in ",;" and depth == 0:
            tokens.append("".join(buf))
            buf = []
        else:
            buf.append(ch)
    tokens.append("".join(buf))
    return tokens


def _clean_token(token):
    """Tek yazar token'ından rolü ayıklar. Döner (isim, rol_or_None). Rol yoksa None."""
    t = _AND_MORE.sub("", token).strip()
    role = None
    m = _ROLE_SUFFIX.search(t)
    if m:
        role = m.group(1).strip()
        t = _ROLE_SUFFIX.sub("", t).strip()
    if t.lower().startswith("by "):
        t = t[3:].strip()
    return t, role


def _parse_authors(raw):
    if not isinstance(raw, str) or not raw.strip():
        return ["Unknown"]

    tokens = _split_top_level(raw)
    parsed = [_clean_token(tok) for tok in tokens if tok.strip()]
    parsed = [(name, role) for (name, role) in parsed if name]

    has_any_role = any(role is not None for _, role in parsed)
    if has_any_role:
        # Rol etiketi var: yalnız "Author" içeren rolü tut; etiketsiz token'ı da yazar say (ör. tek isim).
        authors = [name for name, role in parsed
                   if role is None or "author" in role.lower()]
    else:
        # Hiç rol yok: tüm token'lar yazar.
        authors = [name for name, _ in parsed]

    # Sıra korunarak tekilleştir (aynı kitapta yinelenen ad).
    seen = set()
    deduped = []
    for a in authors:
        if a not in seen:
            seen.add(a)
            deduped.append(a)

    return deduped or ["Unknown"]


def _publisher_for(isbn):
    digest = hashlib.md5(isbn.encode("utf-8")).hexdigest()
    return PUBLISHER_POOL[int(digest, 16) % len(PUBLISHER_POOL)]


def shape(records):
    out = []
    for rec in records:
        isbn = rec.get("isbn")
        if not isbn:
            continue
        out.append({
            "isbn": isbn,
            "title": rec.get("title") or "",
            "authors": _parse_authors(rec.get("brand")),
            "publisher": _publisher_for(isbn),
            "priceTry": rec.get("priceTry"),
            "imageUrl": rec.get("imageUrl"),
            "categoryMid": rec.get("categoryMid"),
            "categoryLeaf": rec.get("categoryLeaf"),
        })
    return out


def main():
    if len(sys.argv) != 2:
        print("usage: shape_books.py <books.json (v1, brand alanlı)>", file=sys.stderr)
        sys.exit(2)

    with open(sys.argv[1], encoding="utf-8") as f:
        records = json.load(f)

    shaped = shape(records)
    json.dump(shaped, sys.stdout, ensure_ascii=False, indent=2)
    print(f"\n# {len(shaped)} kayıt üretildi", file=sys.stderr)


if __name__ == "__main__":
    main()
