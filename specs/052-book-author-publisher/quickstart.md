# Quickstart: Kitap Yazar + Yayınevi Modeli — doğrulama

Feature'ın uçtan-uca çalıştığını kanıtlayan senaryolar. Detaylar: [data-model.md](./data-model.md), [contracts/](./contracts/).

## Ön koşul
- Docker (temiz), Aspire AppHost.
- `shape_books.py` v2 çalıştırılmış, `books.json` v2 üretilmiş.

## 0. Veri şekillendirme (İş1)
Girdi = mevcut committed `books.json` (v1, `brand` alanlı). Ham dataset gerekmez.
```bash
BOOKS=src/services/catalog/Catalog.Api/Seeding/Data/books.json
python3 scripts/book-import/shape_books.py "$BOOKS" > "$BOOKS.tmp" && mv "$BOOKS.tmp" "$BOOKS"
```
**Beklenen (sayım doğrulaması):**
- Her kayıtta `authors` ≥1, `publisher` dolu; `brand`/`contributors` alanı YOK.
- Yayınevi dağılımı yalnız 4 ad; ~dengeli (her biri ~%25).
- Rol-etiketli ham kayıtta yazar-dışı rol sızmamış: `Bill Martin Jr. (Author), Eric Carle (Illustrator)` → `authors=["Bill Martin Jr."]` (Eric Carle atıldı).
- Çok-yazarlı örnek (`Bill Martin Jr. (Author), Eric Carle (Author)`) → `authors` iki eleman.

## 1. Domain birim testleri (İlke VI, önce yazılır)
```bash
dotnet test tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj
```
**Beklenen yeşil:** `Author.Create`/`Publisher.Create` (boş ad hata, normalize), `Product.SetAuthors` (çoklu+dedup+boş red), `Product.SetPublisher`.

## 2. Import + build (canlı)
```bash
dotnet run --project src/aspire/AppHost/AppHost.csproj
```
**Beklenen log:** "Kitap import tamam: N kitap (P yayında, D taslak)". Hata yok.

## 3. Facet — yazar + yayınevi (S1)
```
GET /api/v1/storefront/products/filters
```
- `Authors` dolu, adlarda "(Author)"/"& N more" YOK.
- `Publishers` tam 4 değer.
- `Brands` alanı YOK (kalktı).

## 4. Çok-yazar süzme (S1 — kritik)
```
GET /api/v1/storefront/products/?authorId=<Eric Carle id>
```
- Eric Carle'ın ortak-yazarlı kitabı listede.
- Aynı kitap `authorId=<Bill Martin Jr id>` ile de listede (ortak-yazar kaybı yok).

## 5. Yayınevi süzme (S1)
```
GET /api/v1/storefront/products/?publisherId=<Can id>
```
- Yalnız o yayınevine atanmış kitaplar; hepsinin publisher'ı aynı.

## 6. Künye detay (S2)
```
GET /api/v1/storefront/products/{id}   # çok yazarlı bir kitap
```
- `Authors` liste (tüm yazarlar), `Publisher` tek.
- Yazar-dışı katkıcı alanı yok (kapsam dışı).

## 7. Kararlılık (S3)
- v1 kaynaktan (`git show HEAD:$BOOKS`) shape'i iki kez üret → v2 çıktıları birebir aynı (ISBN→publisher %100, diff boş). Not: in-place v2 çıktısını tekrar besleme (`brand` yok, geçersiz).

**Sonuç (2026-08-29, T027 koşusu — 1427 kitap):**
- Kararlılık: iki koşu **IDENTICAL** (diff boş) — `md5(isbn)%4` deterministik.
- `authors` boş: **0**; `publisher` boş: **0**; `brand` alanı kalan: **0**.
- Distinct publisher = **4** (Can 347 / İletişim 376 / İş Bankası Kültür 337 / Yapı Kredi 367 — ~dengeli).
- Çok-yazarlı kayıt: **11**. Rol-etiketi sızması: **0** (`Sandra Magsamen (Author, Illustrator)`→`["Sandra Magsamen"]`; paren-farkında split ile virgül-içi-rol korunur).

## Başarı kapıları (spec SC ile hizalı)
- SC-001: yazarsız/yayınevisiz kitap %0.
- SC-002: publisher facet = 4.
- SC-003: ortak-yazar her facet'inde görünür.
- SC-004: kullanıcıya görünen adda rol etiketi/kuyruk yok.
- SC-005: import iki-koşu ISBN→publisher %100 aynı.
- SC-006: yazar-dışı roller yazar listesine sızmaz.