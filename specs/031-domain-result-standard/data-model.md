# Phase 1 Data Model: Hedef Metot Envanteri (ECommerce)

Kaynak: `ResultDomain` mevcut — `src/others/Common/Results/ResultDomain.cs:5`.
Kapsam: **handler'dan çağrılan** ham/void aggregate davranış/fabrika metotları (FR-000, Karar 6).

## Refactor hedefleri (10 metot)

| # | Aggregate.Method | Def (file:line) | Şu an döner | Hedef |
|---|------------------|-----------------|-------------|-------|
| 1 | `Basket.StartReservation(DateTimeOffset)` | Baskets/Basket.cs:33 | `void` | `ResultDomain` |
| 2 | `Basket.PurgeExpiredItems(DateTimeOffset)` | Baskets/Basket.cs:36 | `void` | `ResultDomain` |
| 3 | `Basket.AddItem(BasketItem)` | Baskets/Basket.cs:47 | `void` | `ResultDomain` |
| 4 | `Basket.SetItem(...)` | Baskets/Basket.cs:62 | `void` | `ResultDomain` |
| 5 | `Payment.SetStatus(PaymentStatus)` | Payments/Payment.cs:39 | `void` | `ResultDomain` |
| 6 | `Product.Update(...)` | Products/Product.cs:39 | `void` | `ResultDomain` |
| 7 | `ProductStock.Increase(int)` | Stocks/ProductStock.cs:47 | `void` | `ResultDomain` |
| 8 | `ProductStock.Decrease(int)` | Stocks/ProductStock.cs:52 | `void` | `ResultDomain` |
| 9 | `ProductStock.PurgeExpired(DateTimeOffset)` | Stocks/ProductStock.cs:180 | `IReadOnlyList<StockReservation>` | `ResultDomain<IReadOnlyList<StockReservation>>` |
| 10 | `AddressBook.AddAddress(Address)` | AddressBooks/AddressBook.cs:17 | `SavedAddress` | `ResultDomain<SavedAddress>` |

## Çağıran güncellemeleri (10 call-site)

| Metot | Handler (file:line) |
|-------|---------------------|
| PurgeExpiredItems, SetItem, StartReservation | Baskets/Features/Commands/AddBasketItem.cs:35,56,58 |
| PurgeExpiredItems | Baskets/Features/Commands/ClearExpiredBasket.cs:26 |
| SetItem | Baskets/Features/Commands/SetBasketItemQuantity.cs:37,57 |
| AddItem | Baskets/Features/Agent/AddBasketItemForAgent.cs:32 |
| SetStatus | Payments/Features/Commands/CreatePayment.cs:32 |
| Update | Products/Features/Commands/UpdateProduct.cs:49 |
| Increase | Stocks/Features/Commands/IncreaseStock.cs:32 |
| Decrease | Stocks/Features/Commands/DecreaseStock.cs:36 |
| PurgeExpired | Stocks/Features/Scheduled/SweepReservationHandler.cs:25 |
| AddAddress | AddressBooks/Features/Commands/AddAddress.cs:35 |

Not: `SetBasketItemQuantity.cs:37` `RemoveItem` çağırır — o zaten `FeatureResultModel` dönüyor
(uyumlu, dokunulmaz). Sadece `:57 SetItem` hedeftir.

## Test güncellemeleri (5 dosya)

| Test | Satırlar | Metotlar |
|------|----------|----------|
| tests/Basket.Api.Tests/BasketTests.cs | 24,36,46,57,69,85,98,111,128,155,191,204 | AddItem, SetItem, StartReservation, PurgeExpiredItems |
| tests/Stock.Api.Tests/ProductStockTests.cs | 21,31 (+ PurgeExpired) | Increase, Decrease, PurgeExpired |
| tests/Payment.Api.Tests/PaymentTests.cs | SetStatus | SetStatus |
| tests/Catalog.Api.Tests/ProductTests.cs | Update | Update |
| tests/Customer.Api.Tests/AddressBookTests.cs | AddAddress | AddAddress |

## Aggregate-klasör / ValueObjects durumu

- 9 aggregate, her biri kendi `Domains/<X>/` klasöründe — aggregate-per-folder zaten sağlı; taşıma yok.
- Standalone VO → `ValueObjects/` denetimi tasks fazında (envanterde ihlal raporlanmadı).

## Muaf (dokunulmaz)

- Saf getter/sorgu, `RemoveItem` (zaten `FeatureResultModel`), domain service/seeder/read-model.
