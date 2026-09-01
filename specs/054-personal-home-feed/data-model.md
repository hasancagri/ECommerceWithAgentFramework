# Data Model: Kişisel Ana Sayfa (054)

## Yeni doküman: `UserPurchase` (Storefront.Api, storefrontDb)

Read-model birikimi; aggregate DEĞİL (invariant yok, davranış yok — `StorefrontView` emsali).

| Alan | Tip | Not |
|---|---|---|
| `Id` | `string` | Kompozit anahtar `"{UserId:N}:{ProductId:N}"` — `KeyFor(userId, productId)` statik yardımcısı |
| `UserId` | `Guid` | Siparişi veren kullanıcı (`OrderCompleted.UserId`) |
| `ProductId` | `Guid` | Satın alınan ürün (`OrderCompleted.Items[].ProductId`) |

- **Yazım**: yalnız `StorefrontEventHandlers.Handle(OrderCompleted)` — sipariş başına her item
  için `session.Store(...)` (PK upsert = idempotent; FR-002). Quantity/UnitPrice ALINMAZ (YAGNI).
- **Okuma**: yalnız `GetPersonalFeed` query'si — `Where(x => x.UserId == userId)`.
- **Marten kaydı**: `opts.Schema.For<UserPurchase>()`; `UserId` üstüne index
  (`.Index(x => x.UserId)`) — feed sorgusunun tek erişim yolu.
- **Silme/yaşam döngüsü**: yok; profil kalıcı birikir. Backfill yok (Assumptions).

## Değişmeyen: `StorefrontView`

Alan eklenmez, yazıcıları değişmez. Feed sorgusu okur:
`CategoryId`, `Authors[].Id`, `FamilyCode`, `Name`, `Price`, `IsDeleted`, `StockQuantity`,
`RatingAverage`, `ImageUrl` (kart çizimi), `RatingCount`.

## Türetilen (kalıcı olmayan): Kişisel Feed

İstek anında hesaplanır, saklanmaz:

1. `purchases = UserPurchase[userId]` → boşsa boş sonuç (WebApp boş durum çizer).
2. `ownedViews = StorefrontView[purchases.ProductIds]` → `categoryIds`, `authorIds`,
   `ownedFamilyCodes` kümeleri.
3. Adaylar: satılabilir satırlar (Name+Price dolu, `!IsDeleted`) ∧ (`CategoryId ∈ categoryIds`
   ∨ yazar kesişimi) ∧ `ProductId ∉ purchased` ∧ (`FamilyCode ∉ ownedFamilyCodes`).
4. Saf sıralayıcı (test-first birim): yazar eşleşmesi > kategori eşleşmesi; tie →
   `RatingAverage` DESC (null son) → `Name` ASC. Aile gruplama + temsilci: stok>0 önce,
   ucuz önce, `ProductId` ASC. İlk 12 kart.

## İlişkiler

```text
OrderCompleted (Shared.IntegrationEvents, DEĞİŞMEZ)
  └─ UserId + Items[].ProductId ──> UserPurchase (yeni, storefrontDb)
                                        │ userId ile okunur
                                        ▼
                                  GetPersonalFeed ──> StorefrontView (mevcut, salt okunur)
```