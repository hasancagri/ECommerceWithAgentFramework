# Data Model: Tedarikçi Feed'i = Stoğun Tek Otoritesi

**Kalıcı şema değişikliği YOK.** Bu feature yazım topolojisini değiştirir; hiçbir
aggregate/tablo alanı eklenmez veya kaldırılmaz. Değişen tek "veri" servisler-arası
kontrat yükleridir (aşağıda; ayrıntı `contracts/`).

## Değişmeyen aggregate'ler

- **ProductStock (Stock context)** — AYNEN korunur.
  - `Quantity` (OnHand, private set), `_reservations` (aktif hold'lar, TTL'li).
  - Kullanılan mevcut davranışlar: `SetQuantity(int)` (mutlak, negatif-yasak invariant),
    `AvailableAt(now)` (= OnHand − aktif rezervasyon, 0'a kırpılır), `IsOversoldAt(now)`.
  - Yeni metot/alan/invariant EKLENMEZ. StockWrite bu aggregate'e `set_stock` MCP tool'u
    → `SetStock` command → `SetQuantity` üzerinden dokunur (dış dünya kök üzerinden).

- **Product (Catalog context)** — AYNEN korunur. Zaten stok taşımıyordu (`Sku`, `Name`,
  `Price`, `Brand`... ). Değişen yalnız oluşturma çağrısının imzası (stok argümanı düşer),
  aggregate'in kendisi değil.

## Kaldırılan taşıma tipi (payload)

- **ProductStockInfo** `(Guid ProductId, int Quantity)` — SİLİNİR. Tek kullanımı
  `ProductCreatedEvent.Products` idi; o event de kalktığı için tip ölür.

## RecordJob (IngestionAgent iş dosyası) — değişmez

- Mevcut alanlar yeterli: `Message` (kanonik olay, `StockQuantity` taşır), `ProductId?`
  (CatalogWrite doldurur → StockWrite okur), `Failure?`, `Completed`.
- **Yeni alan gerekmez**: StockWrite create/update ayırt etmez (feed her ikisinde de ezer),
  bu yüzden `Action` alanı taşınmaz.

## State geçişleri

- Stok state'i tek yönlü değildir; feed her set'te OnHand'i mutlak değere getirir.
  Rezervasyon/Commit yaşam döngüsü 012'deki gibi kalır (bu feature onları değiştirmez).