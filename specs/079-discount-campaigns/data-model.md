# Phase 1 Data Model: Kampanya İndirim Motoru (Discount.Api)

BC = `discountDb` (Marten document store). Bir aggregate (`Campaign`) + iki read-model
(`ProductDiscount` materyalize kayıt, `ProductCatalogRef` süzgeç-çözüm izdüşümü). **Fiyat alanı YOK**
(İLKE I — Discount.Api saf yüzde otoritesi). BestWins/scope-key/AdminActionLog YOK.

## Aggregate: Campaign

`Campaign : AggregateRoot`. Pencereyi + hangi süzgeçle açıldığını taşır (denetim + iptal + expiry).

| Alan | Tip | Kural |
|---|---|---|
| Id | Guid | AggregateRoot base |
| Name | string | Boş olamaz (admin etiketi) |
| ScopeType | enum {Category, Author, Publisher, Product} | süzgeç tipi; `Campaign.cs` içinde enum |
| ScopeRef | Guid | categoryId / authorId / publisherId / productId |
| Percentage | int | **1-99** (invariant) |
| StartsAt | DateTime (UTC) | |
| EndsAt | DateTime? (UTC) | null = süresiz; dolu ise **> StartsAt** |
| Status | enum {Scheduled, Active, Ended, Cancelled} | Cancelled hard; kalan `now` vs pencereden türetilir |

**Davranış (`ResultDomain` döner):**
- `Create(name, scopeType, scopeRef, percentage, startsAt, endsAt?)` — invariant doğrular.
- `Cancel()` — Status=Cancelled (kitapları temizleme handler'da; scheduled mesaj fire ederse no-op).
- `IsEffectiveAt(now)` (getter, muaf) — Status≠Cancelled && StartsAt≤now && (EndsAt==null||now<EndsAt).

Not: **Edit yok** (v1) — süzgeç snapshot + kitap-başı-tek-indirim modelinde düzenleme karmaşık (hangi
kitaplar yeniden çözülür?); iptal-edip-yeniden-aç yeter. Gerekirse G6.1.

**En-iyi-kazanır helper YOK** — çakışma son-gelen-kazanır ile çözülür (yeni kampanya ezer), hesap gerekmez.

## Read-Model: ProductDiscount (materyalize, kitap başına)

Kampanya uygulanınca her kitap için bir kayıt. **PK = ProductId → kitapta tek ETKİN indirim** (yeni kampanya
kaydı EZER: son-gelen-kazanır, Marten upsert overwrite).

| Alan | Tip | Not |
|---|---|---|
| ProductId | Guid (PK) | teklik = kitapta tek indirim |
| CampaignId | Guid | bağlı kampanya (expiry/iptal bu id'den kitapları bulur) |
| Percentage | int | uygulama anındaki kampanya yüzdesi (snapshot) |
| StartsAt | DateTime | kampanyadan kopyalanır |
| EndsAt | DateTime? | kampanyadan kopyalanır (view-guard + expiry için) |

- **Uygula (yalnız aktifleşmede)**: startsAt≤now ise create'te, gelecek tarihli ise start-fire'da; süzgeç kitap
  setine çözülür → her kitap için `ProductDiscount` **Store** (varsa üzerine yazar; son-gelen-kazanır).
  Scheduled kampanya aktif olana dek YAZMAZ (slot tutmaz).
- **Expiry/İptal**: `campaignId`'ye ait `ProductDiscount`'lar silinir → her biri için `ProductDiscountChanged(pct:0)` it.
- **Checkout gRPC**: `productIds`'e karşı aktif (pencere içi) `ProductDiscount` yüzdeleri döner.

## Destek Read-Model: ProductCatalogRef

Aggregate DEĞİL — Catalog event'inden beslenen izdüşüm; süzgeci (kategori/yazar/yayınevi) kitap setine
çözmek için. Tek-kitap süzgeci bunu kullanmaz (scopeRef zaten productId).

| Alan | Tip | Kaynak (`ProductChangedEvent`) |
|---|---|---|
| ProductId | Guid (PK) | ProductId |
| CategoryId | Guid | CategoryId |
| AuthorIds | Guid[] | Authors[].Id |
| PublisherId | Guid | PublisherId |
| Published | bool | IsDeleted tersi (yayın görünürlüğü) |

`CatalogConsumers.Handle(ProductChangedEvent)` upsert eder. **Fiyat/isim TUTMAZ.**

## Durum Geçişleri (Campaign lifecycle)

```
Create ──(startsAt gelecekte)──▶ Scheduled ──[start fire]──▶ Active ──[end fire / EndsAt geçti]──▶ Ended
Create ──(startsAt≤now)─────────────────────────────────────▶ Active
{Scheduled|Active} ──[admin Cancel]──▶ Cancelled
```
- Fire recompute pencereye bakar, Status'a değil (guard'lı idempotent). Cancelled tek hard durum.

## Akış

```
Catalog ──ProductChangedEvent──▶ Discount.Api (CatalogConsumers → ProductCatalogRef upsert)
Admin ──MCP create_campaign(süzgeç,%,end)──▶ Campaign.Create → ScheduleAsync(start,end)
     ├─ startsAt≤now → AKTİFLEŞTİR (aşağı)
     └─ gelecek     → Scheduled (ProductDiscount YAZMA, slot tutma)
start fire / create-aktif ──▶ AKTİFLEŞTİR:
     → süzgeç → kitap seti (ProductCatalogRef; tek-kitapta doğrudan)
     → her kitap: ProductDiscount Store (varsa üzerine yaz; son-gelen-kazanır)
     → ProductDiscountChanged(pct) it → Storefront ApplyDiscount
end fire ──▶ campaignId kitaplarının ProductDiscount'unu sil ──▶ ProductDiscountChanged(pct:0) ──▶ Storefront temizle
Order checkout ──gRPC GetProductDiscounts(productIds)──▶ aktif ProductDiscount yüzdeleri
```