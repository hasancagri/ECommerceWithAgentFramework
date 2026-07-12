# Data Model: Product Sale Readiness (Completeness Gating)

Kapsam: Catalog bounded context, `Product` aggregate. Yeni entity/value object/event yok.

## Aggregate: Product (mevcut, genişletiliyor)

Mevcut nitelikler (değişmez): `Id`, `Name`, `Description`, `Price`, `Sku`, `Brand (BrandType)`,
`ImageUrl?`, `IsActive` (BaseModel'den, admin aç/kapa), denetim alanları, `IsDeleted`.

### Eklenen durum

| Alan | Tip | Erişim | Anlam |
|------|-----|--------|-------|
| `IsComplete` | `bool` | `get; private set;` (kalıcı) | Ürün bilgisinin tam olup olmadığı. Yalnızca aggregate içinde yeniden hesaplanır. |
| `IsOnSale` | `bool` | `get` (computed, kalıcı değil) | `IsActive && IsComplete`. Response/test için okunur; Marten WHERE'de kullanılmaz. |

### Invariant (aggregate içinde korunur)

- **INV-1 (tamlık):** `IsComplete == (!string.IsNullOrWhiteSpace(Description) && !string.IsNullOrWhiteSpace(ImageUrl))`
  her durum değişiminden sonra her zaman doğru olmalıdır. Dışarıdan `IsComplete` set edilemez.
- **INV-2 (türetme):** `IsOnSale` daima `IsActive && IsComplete`'e eşittir (computed, saklanmaz).

### Davranış metotları

| Metot | Değişiklik | Not |
|-------|-----------|-----|
| `Create(name, description, price, sku, brand, imageUrl)` | Sonunda `RecalculateCompleteness()` çağrılır | Eksik oluşturulan ürün `IsComplete=false` başlar (seed senaryosu). |
| `Update(name, description, price, sku, brand, imageUrl)` | Sonunda `RecalculateCompleteness()` | Açıklama/görsel dolunca `IsComplete=true`; boşalınca `false` (satıştan düşer). |
| `UpdateImageUrl(imageUrl)` | Sonunda `RecalculateCompleteness()` | Yalnızca görsel değişiminde de tamlık güncellenir. |
| `Activate()` / `Deactivate()` | Değişmez | Tamlığı etkilemez; yalnızca `IsActive`'i değiştirir. `IsOnSale` bileşimle güncellenir. |
| `RecalculateCompleteness()` | **YENİ**, `private` | `IsComplete = !IsNullOrWhiteSpace(Description) && !IsNullOrWhiteSpace(ImageUrl)`. |

### Durum geçişleri (satılabilirlik)

```
                 desc & image dolu           desc/image dolu, aktif
  [eksik]  ───────────────────────►  [tam]  ─────────────────────►  [SATIŞTA]
   IsComplete=false                 IsComplete=true                 IsOnSale=true
      ▲                                 │  desc/image boşaltıldı        │ Deactivate()
      └─────────────────────────────────┘◄─────────────────────────────┘
```

- Eksik → hiçbir zaman satışta (IsComplete=false).
- Tam ama pasif → satışta değil (IsActive=false).
- Tam ve aktif → satışta (IsOnSale=true).

## Sorgu projeksiyonları (read tarafı)

| Sorgu | Değişiklik |
|-------|-----------|
| `Agent/SearchProducts` | WHERE: `!IsDeleted && IsActive && IsComplete && Name.Contains(...)` |
| `Agent/GetProduct` | WHERE: `!IsDeleted && IsActive && IsComplete && Name.Contains(...)` |
| `Queries/GetProductByName` | WHERE: `!IsDeleted && IsActive && IsComplete && Name.Contains(...)` |
| `Queries/GetAllProducts` | Filtre değişmez (`!IsDeleted`); `ProductResponse`'a `bool IsComplete` + `bool IsOnSale` alanları eklenir |
| `Queries/GetProductById` | Değişmez (karar: bkz. research Decision 3) |

## Kalıcılık notları

- Marten dokümanı; `IsComplete` JSON'a serialize edilir (Newtonsoft). Non-public setter zaten
  açık (proje ayarı), bu yüzden `private set` deserialize edilir.
- Eski dokümanlar `IsComplete` alanı olmadan `false` gelir (güvenli varsayılan).
- Yeni index gerekmez; Postgres JSONB üzerinde bool filtre yeterli. (İstenirse ileride
  `IsComplete` için computed index eklenebilir; bu feature için zorunlu değil.)